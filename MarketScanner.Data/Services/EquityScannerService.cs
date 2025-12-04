// Normalized after refactor: updated namespace and using references
using MarketScanner.Core.Abstractions;
using MarketScanner.Core.Classification;
using MarketScanner.Core.Configuration;
using MarketScanner.Core.Enums;
using MarketScanner.Core.Filtering;
using MarketScanner.Core.Metadata;
using MarketScanner.Core.Models;
using MarketScanner.Data.Diagnostics;
using MarketScanner.Data.Indicators;
using MarketScanner.Data.Providers;
using MarketScanner.Data.Providers.Finnhub;
using MarketScanner.Data.Services.Alerts;
using MarketScanner.Data.Services.Analysis;
using MarketScanner.Data.Services.Data;
using Polygon.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MarketScanner.Data.Services
{
    public class EquityScannerService : IEquityScannerService
    {
        private const int MinimumCloseCount = 150;
        private const int IndicatorWindow = 120;
        private const int IndicatorPeriod = 14;
        private const int BatchSize = 30;
        private const int MaxConcurrency = 12;
        private CancellationTokenSource? _scanCts;
        private Task? _scanTask;
        private bool _isScanning;
        public bool IsScanning => _isScanning;

        private readonly ManualResetEventSlim _pauseEvent = new(true);
        private readonly SemaphoreSlim _restartLock = new(1,1);
        private readonly AppSettings _settings;
        private readonly IMarketDataProvider _provider;
        private readonly HistoricalPriceCache _priceCache;
        private readonly IAlertManager _alertManager;
        private readonly IAppLogger _logger;
        private readonly IFundamentalProvider _fundamentalProvider;
        private readonly IIndicatorService _indicatorService;
        private readonly IMetadataService _metadataService;
        private readonly IClassificationEngine _classificationEngine;
        private readonly ISymbolScanPipeline _symbolScanPipeline;
        private readonly IFilterService _filterService;
        private readonly IScanController _scanController;
        private readonly IAlertDispatchService _alertDispatchService;
        private readonly TickerMetadataCache _metadataCache;
        private readonly ConcurrentDictionary<string, EquityScanResult> _scanCache = new();
        private readonly List<IEquityClassifier> _classifiers = new();
        private List<IFilter> _filters = new();
        
        public void AddFilter(IFilter filter) => _filterService.AddFilter(filter);
        public void AddMultipleFilters(List<IFilter> filters) => _filterService.AddMultipleFilters(filters);
        public void ClearFilters() => _filterService.ClearFilters();

        public event Action<EquityScanResult>? ScanResultClassified;

        public EquityScannerService(
            IMarketDataProvider provider,
            IFundamentalProvider fundamentalProvider,
            TickerMetadataCache metadataCache,
            IDataCleaner dataCleaner,
            IAlertManager alertManager,
            IAppLogger logger,
            AppSettings settings)
        {
            _settings = settings;
            _provider = provider;
            _fundamentalProvider = fundamentalProvider;
            _alertManager = alertManager;
            _indicatorService = new IndicatorService();
            _logger = logger;
            _priceCache = new HistoricalPriceCache(provider, dataCleaner);
            _fundamentalProvider = fundamentalProvider;
            _metadataCache = metadataCache;
            _metadataService = new MetadataService(metadataCache, provider, fundamentalProvider);
            _classifiers.Add(new RSIClassifier());
            _classificationEngine = new ClassificationEngine(_classifiers);
            _symbolScanPipeline = new SymbolScanPipeline(
                _priceCache,
                _metadataService,
                _indicatorService,
                _classificationEngine,
                _provider,
                _settings);
            _filterService = new FilterService();
            _alertDispatchService = new AlertDispatchService(alertManager);
            _alertDispatchService.ClassificationArrived += r => ScanResultClassified?.Invoke(r);
            _scanController = new ScanController();
            _indicatorService = new IndicatorService();
        }
        public EquityScannerService(IMarketDataProvider provider, IFundamentalProvider fundamentalProvider, TickerMetadataCache metadataCache, IAlertSink alertSink, AppSettings settings)
            : this(
                  provider,
                  fundamentalProvider,
                  metadataCache,
                  CreateDataCleaner(provider, out var logger),
                  CreateAlertManager(logger, alertSink),
                  logger, settings)
        {  
        }

        public ObservableCollection<string> OverboughtSymbols => _alertManager.OverboughtSymbols;
        public ObservableCollection<string> OversoldSymbols => _alertManager.OversoldSymbols;
        public ObservableCollection<string> CreeperSymbols { get; } = new();

        public async Task StartAsync(IProgress<int>? progress = null)
        {
            //Replace with ScanController call
        }
        public async Task RestartAsync(IProgress<int>? progress = null)
        {
            await _restartLock.WaitAsync();
            try
            {
                if(IsScanning)
                {
                    await StopAsync().ConfigureAwait(false);
                }

                await StartAsync(progress).ConfigureAwait(false);
            }
            finally
            {
                _restartLock.Release();
            }
        }

        public async Task StopAsync()
        {
            if (!_isScanning || _scanCts == null)
                return;

            _logger.Log(LogSeverity.Information, "[Scanner] Stopping scan...");
            try
            {
                _scanCts.Cancel();
            }
            catch
            {

            }

            var scanTask = _scanTask;
            if (_scanTask != null)
            {
                try
                {
                    await scanTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {

                }
                catch (Exception ex)
                {
                    _logger.Log(LogSeverity.Error, $"[Scanner] Error during stop: {ex.Message}");
                }
            }
            _scanCts.Dispose();
            _scanCts = null;
            
        }

        public void Pause()
        {
            _pauseEvent.Reset();
        }

        public void Resume()
        {
            _pauseEvent.Set();
        }
        public void SetAlertSink(IAlertSink alertSink)
        {
            _alertManager.SetSink(alertSink);
        }

        public async Task ScanAllAsync(IProgress<int>? progress, CancellationToken cancellationToken)
        {
            Logger.WriteLine("[DEBUG] Clearing scan cache...");
            _scanCache.Clear();
            await _alertManager.ResetAsync().ConfigureAwait(false);

            var tickers = await _provider.GetAllTickersAsync(cancellationToken).ConfigureAwait(false);
            var totalSymbols = tickers.Count;
            if (tickers == null || tickers.Count == 0)
            {
                _logger.Log(LogSeverity.Information, "[Scanner] No tickers available from provider.");
                return;
            }
            _logger.Log(LogSeverity.Information, $"[Scanner] Starting full scan for {tickers.Count:N0} tickers...\nApplied filters: {_filters.Count}");
            foreach(var f in _filters)
            _logger.Log(LogSeverity.Information, $"{f.Name}, ");

            using var limiter = new SemaphoreSlim(MaxConcurrency);
            var tracker = new ScanProgressTracker();

            var tasks = tickers
                .Select(async t =>
                {
                    var enriched = await _metadataService
                    .EnsureMetadataAsync(t, cancellationToken)
                    .ConfigureAwait(false);

                    await ProcessSymbolAsync(
                        enriched,
                        limiter,
                        totalSymbols,
                        tracker,
                        progress,
                        cancellationToken
                        ).ConfigureAwait(false);
                })
                .ToList();

            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
                _metadataCache.SaveCacheToDisk();
            }
            catch (OperationCanceledException)
            {
                _logger.Log(LogSeverity.Information, "[Scanner] Scan cancelled by user.");
            }

            try
            {
                await _alertManager.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await _alertManager.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                progress?.Report(100);
                _logger.Log(LogSeverity.Information, $"[Scanner] Completed. Overbought={_alertManager.OverboughtCount}, Oversold={_alertManager.OversoldCount}");
                //ApplyFilters(_scanCache.Values);
            
            }
            else
            {
                _logger.Log(LogSeverity.Information, "[Scanner] Cancelled mid-run.");
            }
        }


        public async Task<EquityScanResult> ScanSingleSymbol(TickerInfo info)
        {
            return await ScanSingleSymbol(info, CancellationToken.None).ConfigureAwait(false);
        }

        public async Task<EquityScanResult> ScanSingleSymbol(TickerInfo info, CancellationToken cancellationToken)
        {
            string symbol = info.Symbol;
            try
            {
                var result = await _symbolScanPipeline
                    .ScanAsync(info, cancellationToken)
                    .ConfigureAwait(false);

                _scanCache[symbol] = result;
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Log(LogSeverity.Error, $"[Scanner] Failed to fetch {symbol}: {ex.Message}");
                return CreateEmptyResult(symbol);
            }
        }
        public void ClearCache()
        {
            _priceCache.Clear();
            _scanCache.Clear();
        }

        private async Task ProcessSymbolAsync(
            TickerInfo info,
            SemaphoreSlim limiter,
            int totalSymbols,
            ScanProgressTracker tracker,
            IProgress<int>? progress,
            CancellationToken cancellationToken)
        {
            _pauseEvent.Wait(cancellationToken);
            await limiter.WaitAsync(cancellationToken).ConfigureAwait(false);
            string symbol = info.Symbol;
            var (price, volume) = await _provider.GetQuoteAsync(symbol, cancellationToken);
            info.Price = price;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await _symbolScanPipeline
                    .ScanAsync(info, cancellationToken)
                    .ConfigureAwait(false);
                if (_filterService.PassesFilters(result))
                    _alertDispatchService.Dispatch(result);
                _scanCache[symbol] = result;

                var processed = Interlocked.Increment(ref tracker.Processed);
                if (processed % BatchSize == 0 || processed == totalSymbols)
                {
                    try
                    {
                        await _alertManager.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        await _alertManager.FlushAsync(CancellationToken.None).ConfigureAwait(false);
                    }

                    ReportProgress(totalSymbols, processed, tracker, progress);
                }
            }
            catch (OperationCanceledException)
            {
                //_logger.Log(LogSeverity.Information, $"[Scanner] Scan cancelled for {symbol}.");
            }
            catch (Exception ex)
            {
                _logger.Log(LogSeverity.Error, $"[Scanner] Failed to fetch {symbol}: {ex.Message}", ex);
            }
            finally
            {
                limiter.Release();
                try
                {
                    await Task.Delay(25, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            }
        }

        private static void ReportProgress(int totalSymbols, int processed, ScanProgressTracker tracker, IProgress<int>? progress)
        {
            if (totalSymbols == 0)
            {
                return;
            }

            var percentage = (int)((double)processed / totalSymbols * 100);
            var last = Volatile.Read(ref tracker.LastReported);
            if (percentage > last)
            {
                progress?.Report(percentage);
                Interlocked.Exchange(ref tracker.LastReported, percentage);
            }
        }

        private static EquityScanResult CreateEmptyResult(string symbol)
        {
            return new EquityScanResult
            {
                Symbol = symbol,
                Price = double.NaN,
                Volume = double.NaN,
                RSI = double.NaN,
                SMA = double.NaN,
                Upper = double.NaN,
                Lower = double.NaN,
                TimeStamp = DateTime.UtcNow
            };
        }

        private sealed class ScanProgressTracker
        {
            public int Processed;
            public int LastReported;
        }

        private static IDataCleaner CreateDataCleaner(IMarketDataProvider provider, out IAppLogger logger)
        {
            logger = new LoggerAdapter();
            return new DataCleaner(provider, logger);
        }

        private static IAlertManager CreateAlertManager(IAppLogger logger, IAlertSink alertSink)
        {
            var manager = new Alerts.AlertManager(logger, SynchronizationContext.Current);
            manager.SetSink(alertSink);
            return manager;
        }
    }
}
