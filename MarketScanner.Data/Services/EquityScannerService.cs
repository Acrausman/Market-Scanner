using MarketScanner.Data.Models;
using MarketScanner.Data.Providers;
using MarketScanner.Data.Services.Indicators;
using MarketScanner.Data.Diagnostics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MarketScanner.Data.Services
{
    public class EquityScannerService : IEquityScannerService
    {
        private readonly IMarketDataProvider _provider;
        private IAlertSink? _alertSink;
        private readonly ConcurrentDictionary<string, List<double>> _cache = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastFetch = new();

        // UI binding
        public ObservableCollection<string> OverboughtSymbols { get; } = new();
        public ObservableCollection<string> OversoldSymbols { get; } = new();

        public EquityScannerService(IMarketDataProvider provider, IAlertSink alertSink)
        {
            _provider = provider;
            _alertSink = alertSink;

        }

        public async Task ScanAllAsync(IProgress<int>? progress, CancellationToken token)
        {
            // Reset UI state on scan start
            Application.Current.Dispatcher.Invoke(() =>
            {
                OverboughtSymbols.Clear();
                OversoldSymbols.Clear();
            });

            var tickers = await _provider.GetAllTickersAsync().ConfigureAwait(false);
            if (tickers == null || tickers.Count == 0)
            {
                Logger.WriteLine("[Scanner] No tickers available from provider.");
                return;
            }

            Logger.WriteLine($"[Scanner] Starting full scan for {tickers.Count:N0} tickers...");

            using var semaphore = new SemaphoreSlim(12);
            var batch = new ConcurrentBag<(string Symbol, bool Overbought, bool Oversold)>();
            int processed = 0;
            int batchSize = 30;
            int lastReported = 0;

            var tasks = tickers.Select(async symbol =>
            {
                try
                {
                    await semaphore.WaitAsync(token);

                    // Cancel early if requested
                    token.ThrowIfCancellationRequested();

                    var result = await ScanSingleSymbol(symbol).ConfigureAwait(false);
                    Interlocked.Increment(ref processed);

                    if (!double.IsNaN(result.RSI))
                    {
                        bool ob = result.RSI >= 70;
                        bool os = result.RSI <= 30;
                        if (ob || os)
                        {
                            batch.Add((result.Symbol, ob, os));

                            var msg = $"{result.Symbol} is {(ob ? "overbought" : "Oversold")} (RSI{result.RSI:F2})";
                            _alertSink?.AddAlert(msg);
                        }
 
                    }

                    // UI updates in batches
                    if (processed % batchSize == 0 || processed == tickers.Count)
                    {
                        token.ThrowIfCancellationRequested();

                        var localBatch = new List<(string, bool, bool)>();
                        while (batch.TryTake(out var item))
                            localBatch.Add(item);

                        if (localBatch.Count > 0)
                        {
                            await Application.Current.Dispatcher.BeginInvoke(() =>
                            {
                                foreach (var (s, ob, os) in localBatch)
                                {
                                    if (ob)
                                        OverboughtSymbols.Add(s);
                                    else if (os)
                                        OversoldSymbols.Add(s);
                                }
                            });
                        }

                        int pct = (int)((double)processed / tickers.Count * 100);
                        if (pct > lastReported)
                        {
                            progress?.Report(pct);
                            lastReported = pct;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Logger.WriteLine($"[Cancel] {symbol} scan aborted.");
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"[Error] {symbol}: {ex.Message}");
                }
                finally
                {
                    semaphore.Release();
                    // Small delay to avoid hitting Polygon too fast
                    try { await Task.Delay(25, token); } catch { /* ignored */ }
                }
            }).ToList();

            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Logger.WriteLine("[Scanner] Scan cancelled by user.");
            }

            // Final report
            if (!token.IsCancellationRequested)
            {
                progress?.Report(100);
                Logger.WriteLine($"[Scanner] Completed. Overbought={OverboughtSymbols.Count}, Oversold={OversoldSymbols.Count}");
            }
            else
            {
                Logger.WriteLine("[Scanner] Cancelled mid-run.");
            }
        }

        public void SetAlertSink(IAlertSink alertSink)
        {
            _alertSink = alertSink;
        }

        public async Task<EquityScanResult> ScanSingleSymbol(string symbol)
        {
            try
            {
                var closes = await GetCachedClosesAsync(symbol, 150);
                if (closes == null || closes.Count < 15)
                    return new EquityScanResult { Symbol = symbol, RSI = double.NaN };

                closes = closes.TakeLast(120).ToList();
                double rsi = RsiCalculator.Calculate(closes, 14);

                return new EquityScanResult
                {
                    Symbol = symbol,
                    RSI = rsi,
                    TimeStamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"[Error] {symbol}: {ex.Message}");
                return new EquityScanResult { Symbol = symbol, RSI = double.NaN };
            }
        }

        private async Task<List<double>> GetCachedClosesAsync(string symbol, int limit)
        {
            // Use cached data if it's recent enough
            if (_cache.TryGetValue(symbol, out var cached))
            {
                if (cached.Count >= limit && _lastFetch.TryGetValue(symbol, out var ts) &&
                    DateTime.UtcNow - ts < TimeSpan.FromHours(2))
                    return cached.TakeLast(limit).ToList();
            }

            var closes = await _provider.GetHistoricalClosesAsync(symbol, limit + 50);
            if (closes == null || closes.Count == 0)
                return new List<double>();

            _cache[symbol] = closes;
            _lastFetch[symbol] = DateTime.UtcNow;
            return closes.TakeLast(limit).ToList();
        }

        public void ClearCache() => _cache.Clear();
    }
}
