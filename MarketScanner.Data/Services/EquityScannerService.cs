using MarketScanner.Data.Providers;
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
            var tickers = await _provider.GetAllTickersAsync();
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
            lock (_symbolsInProgress)
            {
                if (_symbolsInProgress.Contains(symbol))
                    return; // skip duplicate
                _symbolsInProgress.Add(symbol);
            }

            try
            {
                var closes = await GetCachedClosesAsync(symbol, 30);
                if (closes == null || closes.Count < 14) return;

                double rsi = CalculateRsi(closes);
                if (double.IsNaN(rsi)) return;

                await _uiDispatcher.InvokeAsync(() =>
                {
                    lock (_syncLock)
                    {
                        if (rsi > 70)
                        {
                            Console.WriteLine($"{symbol} is over 70 with an RSI of {rsi}");
                            if (!OverboughtSymbols.Contains(symbol))
                            {
                                OverboughtSymbols.Add(symbol);
                                OversoldSymbols.Remove(symbol);
                            }
                        }
                        else if (rsi < 30)
                        {
                            Console.WriteLine($"{symbol} is under 30 with an RSI of {rsi}");
                            if (!OversoldSymbols.Contains(symbol))
                            {
                                OversoldSymbols.Add(symbol);
                                OverboughtSymbols.Remove(symbol);
                            }
                        }
                        else
                        {
                            Console.WriteLine($"{symbol} is not overbought or oversold with an RSI of {rsi}");
                            OverboughtSymbols.Remove(symbol);
                            OversoldSymbols.Remove(symbol);
                        }
                    }
                }, DispatcherPriority.ContextIdle);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] {symbol}: {ex.Message}");
            }
            finally
            {
                lock (_symbolsInProgress)
                    _symbolsInProgress.Remove(symbol);
            }
        }
        private async Task<List<double>> GetCachedClosesAsync(string symbol, int limit)
        {
            if (_cache.TryGetValue(symbol, out var cached))
                return cached;

            var closes = await _provider.GetHistoricalClosesAsync(symbol, limit);
            _cache[symbol] = closes;
            return closes;
        }

        private static double CalculateRsi(List<double> closes, int period = 14)
        {
            closes = closes.Where(c => !double.IsNaN(c) && c > 0).ToList();
            if (closes.Count <= period) return double.NaN;

            double gain = 0, loss = 0;
            for (int i = 1; i <= period; i++)
            {
                double diff = closes[i] - closes[i - 1];
                if (diff >= 0) gain += diff;
                else loss -= diff;
            }

            double avgGain = gain / period;
            double avgLoss = loss / period;

            if (avgGain == 0 && avgLoss == 0) return 50;
            if (avgLoss == 0) return 100;

            double rs = avgGain / avgLoss;
            return 100 - (100 / (1 + rs));
        }

        public async Task<double> RescanSingleSymbol(string symbol)
        {
            var closes = await GetCachedClosesAsync(symbol, 30);
            if (closes == null || closes.Count < 14) return double.NaN;

            closes = closes.Where(c => c > 0 && !double.IsNaN(c)).ToList();
            if (closes.Count < 14) return double.NaN;

            double rsi = CalculateRsi(closes);
            Console.WriteLine($"[Rescan] {symbol} => RSI={rsi:F2}");
            return rsi;
        }
    }
}
