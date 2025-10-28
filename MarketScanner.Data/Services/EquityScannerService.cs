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
        private readonly ConcurrentDictionary<string, List<double>> _cache =
            new ConcurrentDictionary<string, List<double>>();
        private readonly ConcurrentDictionary<string, DateTime> _lastFetch = new();

        // ViewModel binding
        public ObservableCollection<string> OverboughtSymbols { get; } = new();
        public ObservableCollection<string> OversoldSymbols { get; } = new();

        public EquityScannerService(IMarketDataProvider provider)
        {
            _provider = provider;
        }

        public async Task ScanAllAsync(IProgress<int>? progress, CancellationToken token)
        {
            // clear UI-bound collections on UI thread
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

            Logger.WriteLine($"[Scanner] Starting full scan for {tickers.Count} tickers...");

            int processed = 0;

            var semaphore = new SemaphoreSlim(20); 

            var tasks = tickers.Select(async symbol =>
            {
                await semaphore.WaitAsync(token);
                try
                {
                    token.ThrowIfCancellationRequested();

                    var result = await ScanSingleSymbol(symbol).ConfigureAwait(false);

                    int current = Interlocked.Increment(ref processed);

                    // classify and update ObservableCollections on UI thread
                    if (result.RSI >= 70)
                    {
                        Logger.WriteLine($"{symbol} is overbought with an rsi of {result.RSI}");
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if(symbol != null)
                            OverboughtSymbols.Add(symbol);
                        });
                    }
                    else if (result.RSI <= 30)
                    {
                        Logger.WriteLine($"{symbol} is oversold with an rsi of {result.RSI}");
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if(symbol != null)
                            OversoldSymbols.Add(symbol);
                        });
                    }
                    else
                    {
                        Logger.WriteLine($"{symbol} is neutral at {result.RSI}");
                    }

                    // push progress % back to the VM
                    if (current % 10 == 0 || current == tickers.Count)
                    {
                        int pct = (int)((double)current / tickers.Count * 100);
                        progress?.Report(pct);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToList();

            await Task.WhenAll(tasks).ConfigureAwait(false);

            Logger.WriteLine($"[Scanner] Completed. Overbought={OverboughtSymbols.Count}, Oversold={OversoldSymbols.Count}");

            // final 100% update
            progress?.Report(100);
        }
        public async Task<EquityScanResult> ScanSingleSymbol(string symbol)
        {
            try
            {
                var closes = await GetCachedClosesAsync(symbol, 150);
                if (closes == null || closes.Count < 15)
                {
                    //Console.WriteLine($"[Scan] {symbol}: insufficient data ({closes?.Count ?? 0} bars)");
                    return new EquityScanResult
                    {
                        Symbol = symbol,
                        RSI = double.NaN
                    };
                }

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
            // Always refetch after provider changes or when cache is older than 1 hour
            if (_cache.TryGetValue(symbol, out var cached))
            {
                // Example freshness check
                if (cached.Count >= limit && _lastFetch.TryGetValue(symbol, out var ts) &&
                    DateTime.UtcNow - ts < TimeSpan.FromHours(1))
                    return cached.TakeLast(limit).ToList();
            }

            var closes = await _provider.GetHistoricalClosesAsync(symbol, 150);
            if (closes == null || closes.Count == 0)
                return new List<double>();

            _cache[symbol] = closes;
            _lastFetch[symbol] = DateTime.UtcNow;
            return closes.TakeLast(limit).ToList();
        }

        public void ClearCache() => _cache.Clear();
    }
}
