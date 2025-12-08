// Normalized after refactor: updated namespace and using references
using MarketScanner.Core.Abstractions;
using MarketScanner.Core.Classification;
using MarketScanner.Core.Configuration;
using MarketScanner.Core.Enums;
using MarketScanner.Core.Filtering;
using MarketScanner.Core.Metadata;
using MarketScanner.Core.Models;
using MarketScanner.Core.Progress;
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
        private readonly int MaxConcurrency = 12;
        
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
        private readonly IProgressService _progressService;
        private readonly TickerMetadataCache _metadataCache;
        private readonly ConcurrentDictionary<string, EquityScanResult> _scanCache = new();
        private readonly List<IEquityClassifier> _classifiers = new();
        private List<IFilter> _filters = new();
        
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
            _progressService = new ProgressService();
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

        public Task StartAsync(IProgress<int>? progress = null)
        {
            return _scanController.StartAsync(
                token => ScanAllAsync(progress, token),
                progress);
        }
        public Task RestartAsync(IProgress<int>? progress = null)
        {
            return _scanController.RestartAsync(
                token => ScanAllAsync(progress, token),
                progress);
        }

        public Task StopAsync()
        {
            return _scanController.StopAsync();
        }

        public void Pause() => _scanController.Pause();

        public void Resume() => _scanController.Resume();
        public void SetAlertSink(IAlertSink alertSink)
        {
            _alertManager.SetSink(alertSink);
        }

        public async Task ScanAllAsync(IProgress<int>? progress, CancellationToken cancellationToken)
        {
            var tickers = await _provider.GetAllTickersAsync(cancellationToken)
                .ConfigureAwait(false);

            int totalSymbols = tickers.Count;
            var tracker = _progressService.CreateTracker(totalSymbols);
            using var limiter = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);

            var tasks = tickers.Select(info =>
                ProcessSymbolAsync(info, limiter, tracker, progress, cancellationToken));

            await Task.WhenAll(tasks).ConfigureAwait(false);

            progress?.Report(100);
            await _alertManager
                .FlushAsync(cancellationToken)
                .ConfigureAwait(false);
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
            ScanProgressTracker tracker,
            IProgress<int>? progress,
            CancellationToken cancellationToken)
        {
            _scanController.WaitForResume(cancellationToken);
            await limiter.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var result = await _symbolScanPipeline.ScanAsync(info, cancellationToken)
                    .ConfigureAwait(false);
                if (_filterService.PassesFilters(result))
                    _alertDispatchService.Dispatch(result);
                _scanCache[info.Symbol] = result;

                _progressService.Increment(tracker);
                _progressService.TryReport(tracker, progress);

                await _alertManager.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.Log(LogSeverity.Error, $"[Scanner] Failed to fetch {info.Symbol}: {ex.Message}", ex);
            }
            finally
            {
                limiter.Release();
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
