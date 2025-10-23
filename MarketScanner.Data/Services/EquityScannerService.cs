using MarketScanner.Data.Models;
using MarketScanner.Data.Providers;
using MarketScanner.Data.Services.Indicators;
using MarketScanner.Data.Diagnostics;
using Microsoft.VisualBasic;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MarketScanner.Data.Services
{
    public class EquityScannerService
    {
        private readonly IMarketDataProvider _provider;
        private readonly ConcurrentDictionary<string, List<double>> _cache =
            new ConcurrentDictionary<string, List<double>>();
        private readonly ConcurrentDictionary<string, DateTime> _lastFetch = new();

        // 🧩 Added: observable collections for ViewModel binding
        public ObservableCollection<string> OverboughtSymbols { get; } = new();
        public ObservableCollection<string> OversoldSymbols { get; } = new();

        public EquityScannerService(IMarketDataProvider provider)
        {
            _provider = provider;
        }

        /// <summary>
        /// Scans all tickers with UI progress and cancellation support.
        /// </summary>
        public async Task ScanAllAsync(IProgress<int> progress, CancellationToken token)
        {
            OverboughtSymbols.Clear();
            OversoldSymbols.Clear();

            var tickers = await _provider.GetAllTickersAsync().ConfigureAwait(false);
            if (tickers == null || tickers.Count == 0)
            {
                Logger.WriteLine("[Scanner] No tickers available from provider.");
                return;
            }

            Logger.WriteLine($"[Scanner] Starting full scan for {tickers.Count} tickers...");

            int processed = 0;

            var tasks = tickers.Select(async symbol =>
            {
                token.ThrowIfCancellationRequested();

                var result = await ScanSingleSymbol(symbol).ConfigureAwait(false);
                Interlocked.Increment(ref processed);

                if (result.RSI >= 70)
                {
                    Logger.WriteLine($"{symbol} is overbought with an rsi of {result.RSI}");
                    lock (OverboughtSymbols)
                        OverboughtSymbols.Add(symbol);
                }
                else if (result.RSI <= 30)
                {
                    Logger.WriteLine($"{symbol} is oversold with an rsi of {result.RSI}");
                    lock (OversoldSymbols)
                        OversoldSymbols.Add(symbol);
                }
                else
                {
                    Logger.WriteLine($"{symbol} is neutral at {result.RSI}");
                }

                    progress?.Report((int)((double)processed / tickers.Count * 100));
            }).ToList(); // force enumeration

            await Task.WhenAll(tasks).ConfigureAwait(false);

            Logger.WriteLine($"[Scanner] Completed. Overbought={OverboughtSymbols.Count}, Oversold={OversoldSymbols.Count}");
        }

        /// <summary>
        /// Scans a single symbol for RSI classification.
        /// </summary>
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

                //Console.WriteLine($"[RSI Test] {symbol} closes: {string.Join(",", closes.TakeLast(5).Select(v => v.ToString("F2")))}");
                //Console.WriteLine($"[Debug RSI] {symbol}   RSI={rsi:F2}");

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

        /// <summary>
        /// Retrieves or caches historical closes.
        /// </summary>
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

        /// <summary>
        /// Clears the in-memory cache (optional).
        /// </summary>
        public void ClearCache() => _cache.Clear();
    }
}
