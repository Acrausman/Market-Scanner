// Normalized after refactor: updated namespace and using references
using MarketScanner.Core.Abstractions;
using MarketScanner.Core.Configuration;
using MarketScanner.Core.Models;
using MarketScanner.Data.Diagnostics;
using MarketScanner.Data.Providers;
using MarketScanner.Data.Services.Alerts;
using MarketScanner.Data.Services.Analysis;
using MarketScanner.Data.Services.Data;
using MarketScanner.Data.Indicators;
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

        private readonly ManualResetEventSlim _pauseEvent = new(true);
        private readonly AppSettings _settings;
        private readonly IMarketDataProvider _provider;
        private readonly HistoricalPriceCache _priceCache;
        private readonly IAlertManager _alertManager;
        private readonly IAppLogger _logger;
        private readonly ConcurrentDictionary<string, EquityScanResult> _scanCache = new();

        public EquityScannerService(
            IMarketDataProvider provider,
            IDataCleaner dataCleaner,
            IAlertManager alertManager,
            IAppLogger logger,
            AppSettings settings)
        {
            _settings = settings;
            _provider = provider;
            _alertManager = alertManager;
            _logger = logger;
            _priceCache = new HistoricalPriceCache(provider, dataCleaner);
        }

        public EquityScannerService(IMarketDataProvider provider, IAlertSink alertSink, AppSettings settings)
            : this(
                  provider,
                  CreateDataCleaner(provider, out var logger),
                  CreateAlertManager(logger, alertSink),
                  logger, settings)
        {
        }

        public ObservableCollection<string> OverboughtSymbols => _alertManager.OverboughtSymbols;
        public ObservableCollection<string> OversoldSymbols => _alertManager.OversoldSymbols;

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
            await _alertManager.ResetAsync().ConfigureAwait(false);

            var tickers = await _provider.GetAllTickersAsync(cancellationToken).ConfigureAwait(false);
            if (tickers == null || tickers.Count == 0)
            {
                _logger.Log(LogSeverity.Information, "[Scanner] No tickers available from provider.");
                return;
            }

            _logger.Log(LogSeverity.Information, $"[Scanner] Starting full scan for {tickers.Count:N0} tickers...");

            using var limiter = new SemaphoreSlim(MaxConcurrency);
            var tracker = new ScanProgressTracker();

            var tasks = tickers
                .Select(symbol => ProcessSymbolAsync(symbol, limiter, tickers.Count, tracker, progress, cancellationToken))
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
            }
            else
            {
                _logger.Log(LogSeverity.Information, "[Scanner] Cancelled mid-run.");
            }
        }

        public async Task<EquityScanResult> ScanSingleSymbol(string symbol)
        {
            return await ScanSingleSymbol(symbol, CancellationToken.None).ConfigureAwait(false);
        }

        public async Task<EquityScanResult> ScanSingleSymbol(string symbol, CancellationToken cancellationToken)
        {
            try
            {
                var result = await ScanSymbolCoreAsync(symbol, cancellationToken).ConfigureAwait(false);
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

        public void ClearCache()
        {
            _priceCache.Clear();
            _scanCache.Clear();
        }

        private async Task ProcessSymbolAsync(
            string symbol,
            SemaphoreSlim limiter,
            int totalSymbols,
            ScanProgressTracker tracker,
            IProgress<int>? progress,
            CancellationToken cancellationToken)
        {
            _pauseEvent.Wait(cancellationToken);
            await limiter.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await ScanSymbolCoreAsync(symbol, cancellationToken).ConfigureAwait(false);
                _scanCache[symbol] = result;

                QueueAlerts(result);

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
                _logger.Log(LogSeverity.Information, $"[Scanner] Scan cancelled for {symbol}.");
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

        private void QueueAlerts(EquityScanResult result)
        {
            if (double.IsNaN(result.RSI))
            {
                return;
            }

            if (result.RSI >= 70)
            {
                _alertManager.Enqueue(result.Symbol, "overbought", result.RSI);
            }
            else if (result.RSI <= 30)
            {
                _alertManager.Enqueue(result.Symbol, "oversold", result.RSI);
            }
        }

        private async Task<EquityScanResult> ScanSymbolCoreAsync(string symbol, CancellationToken cancellationToken)
        {
            var closes = await _priceCache.GetClosingPricesAsync(symbol, MinimumCloseCount, cancellationToken).ConfigureAwait(false);
            if (closes == null || closes.Count < IndicatorPeriod)
            {
                _logger.Log(LogSeverity.Warning, $"[Scanner] Skipping {symbol} due to missing data.");
                return CreateEmptyResult(symbol);
            }

            var trimmed = closes.Skip(Math.Max(0, closes.Count - IndicatorWindow)).ToList();
            if (trimmed.Count < IndicatorPeriod)
            {
                _logger.Log(LogSeverity.Warning, $"[Scanner] Skipping {symbol} due to missing data.");
                return CreateEmptyResult(symbol);
            }
            //_logger.Log(LogSeverity.Debug, $"[Settings] Using RSI method: {_settings.RsiMethod}");
            var rsi = RsiCalculator.Calculate(trimmed, IndicatorPeriod, _settings.RsiMethod);
            var sma = SmaCalculator.Calculate(trimmed, IndicatorPeriod);
            var (_, upper, lower) = BollingerBandsCalculator.Calculate(trimmed, IndicatorPeriod);

            var (price, volume) = await _provider.GetQuoteAsync(symbol, cancellationToken).ConfigureAwait(false);

            if(rsi > 70)
            {
                _logger.Log(LogSeverity.Debug, $"{symbol} last 30 closes: {string.Join(", ", trimmed.TakeLast(30))}");
                _logger.Log(LogSeverity.Debug, $"{symbol} RSI is overbought at: {rsi}");
            }
            else if(rsi < 30)
            {
                _logger.Log(LogSeverity.Debug, $"{symbol} last 30 closes: {string.Join(", ", trimmed.TakeLast(30))}");
                _logger.Log(LogSeverity.Debug, $"{symbol} RSI is oversold at: {rsi}");
            }
                return new EquityScanResult
                {
                    Symbol = symbol,
                    Price = double.IsNaN(price) ? trimmed.LastOrDefault() : price,
                    Volume = volume,
                    RSI = rsi,
                    SMA = sma,
                    Upper = upper,
                    Lower = lower,
                    TimeStamp = DateTime.UtcNow
                };
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
