using MarketScanner.Data.Providers;
using MarketScanner.Data.Services.Indicators;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace MarketScanner.Data.Services
{
    public class EquityScannerService
    {
        private readonly IMarketDataProvider _provider;
        private readonly SemaphoreSlim _semaphore = new(8); // limit API concurrency
        private readonly Dictionary<string, List<double>> _cache = new();
        private readonly Dispatcher _uiDispatcher; // ✅ safe dispatcher reference
        private readonly object _syncLock = new();

        public ObservableCollection<string> OverboughtSymbols { get; } = new();
        public ObservableCollection<string> OversoldSymbols { get; } = new();

        public EquityScannerService(IMarketDataProvider provider)
        {
            _provider = provider;

            // If Application.Current is null (unit test / startup), fall back to a static dispatcher
            if (Application.Current != null)
                _uiDispatcher = Application.Current.Dispatcher;
            else
                _uiDispatcher = Dispatcher.FromThread(Thread.CurrentThread) ?? Dispatcher.CurrentDispatcher;
        }

        public IMarketDataProvider Provider => _provider;

        

        public async Task ScanAllAsync(IProgress<int> progress, CancellationToken token)
        {
            Console.WriteLine("[Scanner] Fetching all tickers...");
            _uiDispatcher.Invoke(() =>
            {
                OverboughtSymbols.Clear();
                OversoldSymbols.Clear();
            });

            var tickers = await _provider.GetAllTickersAsync();
            tickers = tickers
                .Where(t => t.Length <= 5 && !t.Contains('.') && t.All(ch => ch is >= 'A' and <= 'Z'))
                .Where(t => !t.EndsWith("W") && !t.EndsWith("R")).ToList();

            Console.WriteLine($"[Scanner] Found {tickers.Count} active tickers.");

            int total = tickers.Count;
            int processed = 0;

            var tasks = new List<Task>();

            foreach (var symbol in tickers)
            {
                if (token.IsCancellationRequested) break;

                await _semaphore.WaitAsync(token);
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await ScanSingleSymbol(symbol);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Scanner] Error {symbol}: {ex.Message}");
                    }
                    finally
                    {
                        Interlocked.Increment(ref processed);
                        progress?.Report((int)((processed / (double)total) * 100));
                        _semaphore.Release();
                    }
                }, token));
            }

            await Task.WhenAll(tasks);
            Console.WriteLine("[Scanner] ✅ Full scan complete!");
        }

        private readonly HashSet<string> _symbolsInProgress = new();

        public async Task ScanSingleSymbol(string symbol)
        {
            try
            {
                // 🔹 Fetch historical closes — skip cache for debugging (use cache later once verified)
                var closesForUi = await GetCachedClosesAsync(symbol, 30);
                var closesForRsi = await GetCachedClosesAsync(symbol, 120);
                if (closesForRsi == null || closesForRsi.Count <15) return;

                closesForRsi = closesForRsi.Where(c => c > 0 && !double.IsNaN(c)).ToList();
                if (closesForRsi.Count < 15) return;

                Console.WriteLine($"[RSI Test] {symbol} closes: {string.Join(',', closesForRsi.TakeLast(5).Select(c => c.ToString("F2")))}");
                // 🔹 Calculate RSI
                double rsi = RsiCalculator.Calculate(closesForRsi);
                double uRsi = RsiCalculator.Calculate(closesForUi);
                if (double.IsNaN(rsi))
                {
                    Console.WriteLine($"[SkipTicker] {symbol}: RSI invalid");
                    return;
                }

                // 🔹 Sanity check: if RSI seems unrealistic, skip
                if (rsi < 0 || rsi > 100)
                {
                    Console.WriteLine($"[SkipTicker] {symbol}: out-of-range RSI={rsi:F2}");
                    return;
                }

                // 🔹 Diagnostic logging for confirmation
                Console.WriteLine($"[Debug RSI] {symbol,-6} RSI={rsi,6:F2} | LastCloses=...{string.Join(",", closesForRsi.TakeLast(5).Select(c => c.ToString("F2")))}");

                // 🔹 Apply UI update with strong locking
                lock (_syncLock)
                {
                    _uiDispatcher.Invoke(() =>
                    {
                        if (rsi > 70)
                        {
                            Console.WriteLine($"[UI Update] {symbol} -> Overbought ({rsi:F2})");
                            if (!OverboughtSymbols.Contains(symbol))
                            {
                                OverboughtSymbols.Add(symbol);
                                OversoldSymbols.Remove(symbol);
                            }
                        }
                        else if (rsi < 30)
                        {
                            Console.WriteLine($"[UI Update] {symbol} -> Oversold ({rsi:F2})");
                            if (!OversoldSymbols.Contains(symbol))
                            {
                                OversoldSymbols.Add(symbol);
                                OverboughtSymbols.Remove(symbol);
                            }
                        }
                        else
                        {
                            Console.WriteLine($"[UI Update] {symbol} -> Neutral ({rsi:F2})");
                            OverboughtSymbols.Remove(symbol);
                            OversoldSymbols.Remove(symbol);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] {symbol}: {ex.Message}");
            }
        }

        private async Task<List<double>> GetCachedClosesAsync(string symbol, int limit)
        {
            // Each symbol gets its own independent copy
            if (_cache.TryGetValue(symbol, out var cached))
                return new List<double>(cached); // clone to avoid mutation

            var closes = await _provider.GetHistoricalClosesAsync(symbol, limit);

            // Defensive copy and null check
            if (closes == null || closes.Count == 0)
                return new List<double>();

            _cache[symbol] = new List<double>(closes);
            return closes;
        }


        private static double CalculateRsi(List<double> closes, int period = 14)
        {
            if (closes == null || closes.Count <= period)
                return double.NaN;

            double gain = 0, loss = 0;

            // initialize with first 'period' differences
            for (int i = 1; i <= period; i++)
            {
                double diff = closes[i] - closes[i - 1];
                if (diff >= 0)
                    gain += diff;
                else
                    loss -= diff;
            }

            double avgGain = gain / period;
            double avgLoss = loss / period;

            // Wilder's smoothing formula
            for (int i = period + 1; i < closes.Count; i++)
            {
                double diff = closes[i] - closes[i - 1];
                if (diff >= 0)
                {
                    avgGain = ((avgGain * (period - 1)) + diff) / period;
                    avgLoss = ((avgLoss * (period - 1)) + 0) / period;
                }
                else
                {
                    avgGain = ((avgGain * (period - 1)) + 0) / period;
                    avgLoss = ((avgLoss * (period - 1)) - diff) / period;
                }
            }

            if (avgLoss == 0) return 100;

            double rs = avgGain / avgLoss;
            return 100 - (100 / (1 + rs));
        }
        /*
        public async Task<double> RescanSingleSymbol(string symbol)
        {
            var closes = await GetCachedClosesAsync(symbol, 120);
            if (closes == null || closes.Count < 14) return double.NaN;

            closes = closes.Where(c => c > 0 && !double.IsNaN(c)).ToList();
            if (closes.Count < 14) return double.NaN;

            double rsi = RsiCalculator.Calculate(closes);
            Console.WriteLine($"[Rescan] {symbol} => RSI={rsi:F2}");
            // inside ScanSingleSymbol after rsi = ...
            Application.Current.Dispatcher.Invoke(() =>
            {
                // remove from both first
                OverboughtSymbols.Remove(symbol);
                OversoldSymbols.Remove(symbol);

                // small hysteresis helps (e.g., 71/29)
                if (rsi >= 71 && !OverboughtSymbols.Contains(symbol))
                    OverboughtSymbols.Add(symbol);
                else if (rsi <= 29 && !OversoldSymbols.Contains(symbol))
                    OversoldSymbols.Add(symbol);
            });

            return rsi;
        }
        */
    }
}
