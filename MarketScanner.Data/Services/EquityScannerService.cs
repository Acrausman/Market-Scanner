// Normalized after refactor: updated namespace and using references
using MarketScanner.Core.Abstractions;
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
        private readonly AppSettings _settings;
        private readonly IMarketDataProvider _provider;
        private readonly HistoricalPriceCache _priceCache;
        private readonly IAlertManager _alertManager;
        private readonly IAppLogger _logger;
        private readonly IFundamentalProvider _fundamentalProvider;
        private readonly TickerMetadataCache _metadataCache;
        private readonly ConcurrentDictionary<string, EquityScanResult> _scanCache = new();
        //private readonly string FinnApiKey = "d44drfhr01qt371uia8gd44drfhr01qt371uia90";
        private readonly List<IFilter> _filters = new();

        public event Action<EquityScanResult>? ScanResultClassified;
        public void AddFilter(IFilter filter)
        {
            _logger.Log(LogSeverity.Information, $"Adding {filter} to filters\n Active filters:");
            _filters.Add(filter);
            foreach (var f in _filters )_logger.Log(LogSeverity.Information, f.Name);
        }
        public void ClearFilters()
            {
            _logger.Log(LogSeverity.Information, "Filters cleared");
            _filters.Clear();
            }

        public EquityScannerService(
            IMarketDataProvider provider,
            IFundamentalProvider fundamentalProvider,
            IDataCleaner dataCleaner,
            IAlertManager alertManager,
            IAppLogger logger,
            AppSettings settings)
        {
            _settings = settings;
            _provider = provider;
            _fundamentalProvider = fundamentalProvider;
            _alertManager = alertManager;
            _logger = logger;
            _priceCache = new HistoricalPriceCache(provider, dataCleaner);
            _fundamentalProvider = fundamentalProvider;
            _metadataCache = new TickerMetadataCache("ticker_metadata.json");
        }
        public EquityScannerService(IMarketDataProvider provider, IFundamentalProvider fundamentalProvider, IAlertSink alertSink, AppSettings settings)
            : this(
                  provider,
                  fundamentalProvider,
                  CreateDataCleaner(provider, out var logger),
                  CreateAlertManager(logger, alertSink),
                  logger, settings)
        {  
        }

        public ObservableCollection<string> OverboughtSymbols => _alertManager.OverboughtSymbols;
        public ObservableCollection<string> OversoldSymbols => _alertManager.OversoldSymbols;
        public ObservableCollection<TickerInfo> FilteredSymbols { get; } = new();

        public async Task StartAsync(IProgress<int>? progress = null)
        {
            if (_isScanning)
            {
                _logger.Log(LogSeverity.Warning, "[Scanner] A scan is already running.");
                return;
            }

            _isScanning = true;
            _scanCts = new CancellationTokenSource();
            var token = _scanCts.Token;

            _logger.Log(LogSeverity.Information, "[Scanner] Starting scan...");
            _scanTask = Task.Run(async () =>
            {
                try
                {
                    await ScanAllAsync(progress, token);
                }
                catch (OperationCanceledException)
                {
                    _logger.Log(LogSeverity.Information, "[Scanner] Scan cancelled");
                }
                catch (Exception ex)
                {
                    _logger.Log(LogSeverity.Error, $"[Scanner] Scan failed: {ex.Message}");
                }
                finally
                {
                    _isScanning = false;
                    _logger.Log(LogSeverity.Information, "[Scanner] Scan finished.");
                }
            });

        }

        public async Task StopAsync()
        {
            if (!_isScanning || _scanCts == null)
                return;

            _logger.Log(LogSeverity.Information, "[Scanner] Stopping scan...");
            _scanCts.Cancel();

            if (_scanTask != null)
                await _scanTask;

            _isScanning = false;
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

            using var limiter = new SemaphoreSlim(MaxConcurrency);
            var tracker = new ScanProgressTracker();

            var tasks = tickers
                .Select(ticker => ProcessSymbolAsync(ticker, limiter, totalSymbols, tracker, progress, cancellationToken))
                .ToList();

            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
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
                var result = await ScanSymbolCoreAsync(info, cancellationToken).ConfigureAwait(false);
                _scanCache[symbol] = result;
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Log(LogSeverity.Error, $"[Scanner] Failed to fetch {symbol}: {ex.Message}", ex);
                return CreateEmptyResult(symbol);
            }
        }

        private void ApplyFilters(TickerInfo info)
        {
            /*Logger.Info($"Country and sector for {info.Symbol} are {info.Country} and {info.Sector} respectively." +
                $"Exchange is {info.Exchange}");*/
            if (_filters.Count == 0)
                return;
            bool matchesAll = _filters.All(f => f.Matches(info));
            if(matchesAll)
            {
                lock (FilteredSymbols)
                {
                    FilteredSymbols.Add(info);
                }
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
                var result = await ScanSymbolCoreAsync(info, cancellationToken).ConfigureAwait(false);
                QueueAlerts(result);
                ApplyFilters(info);
                _scanCache[symbol] = result;
                Logger.WriteLine($"[CACHE CHECK] {symbol} → sector={_scanCache[symbol].Sector}, country={_scanCache[symbol].Country}");

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
                //_logger.Log(LogSeverity.Error, $"[Scanner] Failed to fetch {symbol}: {ex.Message}", ex);
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

        private void QueueAlerts(EquityScanResult result)
        {
            if (double.IsNaN(result.RSI))
            {
                return;
            }

            if (result.RSI >= 70)
            {
                _alertManager.Enqueue(result.Symbol, "overbought", result.RSI);
                ScanResultClassified?.Invoke(result);
            }
            else if (result.RSI <= 30)
            {
                _alertManager.Enqueue(result.Symbol, "oversold", result.RSI);
                ScanResultClassified?.Invoke(result);
            }

        }

        private async Task<EquityScanResult> ScanSymbolCoreAsync(TickerInfo info, CancellationToken cancellationToken)
        {
            string symbol = info.Symbol;
            var closes = await _priceCache.GetClosingPricesAsync(symbol, MinimumCloseCount, cancellationToken).ConfigureAwait(false);
            if (closes == null || closes.Count < IndicatorPeriod)
            {
                //_logger.Log(LogSeverity.Warning, $"[Scanner] Skipping {symbol} due to missing data.");
                return CreateEmptyResult(symbol);
            }

            var trimmed = closes.Skip(Math.Max(0, closes.Count - IndicatorWindow)).ToList();
            if (trimmed.Count < IndicatorPeriod)
            {
                //_logger.Log(LogSeverity.Warning, $"[Scanner] Skipping {symbol} due to missing data.");
                return CreateEmptyResult(symbol);
            }
            TickerInfo? meta;
            bool hasCached = _metadataCache.TryGet(symbol, out meta);

            // Treat “Unknown” as not good enough – force enrichment
            bool needsEnrich =
                !hasCached ||
                meta == null ||
                string.IsNullOrWhiteSpace(meta.Country) ||
                meta.Country == "Unknown" ||
                string.IsNullOrWhiteSpace(meta.Sector) ||
                meta.Sector == "Unknown";

            if (needsEnrich && _fundamentalProvider != null)
            {
                var fetched = await _fundamentalProvider
                    .GetMetadataAsync(symbol, cancellationToken)
                    .ConfigureAwait(false);

                if (fetched != null)
                {
                    meta = fetched;
                    _metadataCache.AddOrUpdate(fetched);
                }
            }

            var rsiMethod = _settings?.RsiMethod ?? RsiSmoothingMethod.Simple;
            var rsi = RsiCalculator.Calculate(trimmed, IndicatorPeriod, rsiMethod);
            var sma = SmaCalculator.Calculate(trimmed, IndicatorPeriod);
            var (_, upper, lower) = BollingerBandsCalculator.Calculate(trimmed, IndicatorPeriod);

            var (price, volume) = await _provider.GetQuoteAsync(symbol, cancellationToken)
                .ConfigureAwait(false);

            var result = new EquityScanResult
            {
                Symbol = symbol,
                Price = double.IsNaN(price) ? trimmed.LastOrDefault() : price,
                Volume = volume,
                RSI = rsi,
                SMA = sma,
                Upper = upper,
                Lower = lower,
                TimeStamp = DateTime.UtcNow,
                Sector = meta?.Sector ?? "Unknown",
                Country = meta?.Country ?? "Unknown"
            };

            Logger.WriteLine($"Sector and country for {symbol} are {result.Sector} and {result.Country}");
            return result;

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
