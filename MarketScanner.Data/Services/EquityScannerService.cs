using MarketScanner.Data.Providers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MarketScanner.Data.Services
{
    public class EquityScannerService
    {
        private readonly IMarketDataProvider _provider;
        private readonly SemaphoreSlim _semaphore;
        private readonly Dictionary<string, List<double>> _cache = new();

        public EquityScannerService(IMarketDataProvider dataProvider)
        {
            _provider = dataProvider;
            _semaphore = new SemaphoreSlim(5); // up to 5 concurrent requests
        }

        public ObservableCollection<string> OverboughtSymbols { get; } = new();
        public ObservableCollection<string> OversoldSymbols { get; } = new();

        // --- MAIN SCAN METHOD ---
        public async Task ScanAllAsync(IProgress<int> progress, CancellationToken token)
        {
            Console.WriteLine("Fetching all tickers...");
            var tickers = await _provider.GetAllTickersAsync();
            Console.WriteLine($"Found {tickers.Count} active symbols.");

            const int batchSize = 100;
            int total = tickers.Count;
            int processed = 0;

            foreach (var batch in tickers.Chunk(batchSize))
            {
                await ProcessBatchAsync(batch, progress, token);
                processed += batch.Length;
                progress?.Report(processed * 100 / total);

                Console.WriteLine($"Processed {processed}/{total} symbols...");
            }

            Console.WriteLine("Scan complete!");
        }

        // --- BATCH PROCESSING WITH THROTTLING ---
        private async Task ProcessBatchAsync(IEnumerable<string> symbols,
                                             IProgress<int>? progress,
                                             CancellationToken token)
        {
            var tasks = symbols.Select(async s =>
            {
                await _semaphore.WaitAsync(token);
                try
                {
                    var closes = await GetCachedClosesAsync(s, 30, token);
                    await Task.Delay(200, token); // rate-limit

                    if (closes.Count >= 15)
                    {
                        var rsi = CalculateRsi(closes);
                        if (rsi > 70)
                            Application.Current.Dispatcher.BeginInvoke(() => OverboughtSymbols.Add(s));
                        else if (rsi < 30)
                            Application.Current.Dispatcher.BeginInvoke(() => OversoldSymbols.Add(s));

                        Console.WriteLine($"Processed {s}: RSI={rsi:F2}");
                    }
                    else
                    {
                        Console.WriteLine($"{s}: insufficient bars ({closes.Count})");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{s}] {ex.Message}");
                }
                finally
                {
                    _semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        // --- CACHING LAYER ---
        private async Task<List<double>> GetCachedClosesAsync(string symbol, int limit, CancellationToken ct)
        {
            if (_cache.TryGetValue(symbol, out var cached))
                return cached;

            var closes = await _provider.GetHistoricalClosesAsync(symbol, limit);
            _cache[symbol] = closes;
            return closes;
        }

        // --- RSI CALCULATION ---
        private double CalculateRsi(List<double> closes, int period = 14)
        {
            closes = closes.Where(c => !double.IsNaN(c) && c > 0).ToList();
            if (closes.Count < period + 1)
                return double.NaN;

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
            double rsi = 100 - (100 / (1 + rs));

            return Math.Clamp(rsi, 0, 100);
        }
    }
}
