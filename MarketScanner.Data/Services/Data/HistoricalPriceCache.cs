using MarketScanner.Core.Models;
using MarketScanner.Data.Providers;
using MarketScanner.Data.Services.Analysis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MarketScanner.Data.Services.Data
{
    internal class HistoricalPriceCache
    {
        private readonly IMarketDataProvider _provider;
        private readonly IDataCleaner _dataCleaner;
        private readonly ConcurrentDictionary<string, CachedSeries> _cache = new();

        public HistoricalPriceCache(IMarketDataProvider provider, IDataCleaner dataCleaner)
        {
            _provider = provider;
            _dataCleaner = dataCleaner;
        }

        public async Task<IReadOnlyList<double>> GetClosingPricesAsync(string symbol, int minimumCount, CancellationToken cancellationToken)
        {
            var cachedSeries = GetCachedSeries(symbol);
            if (cachedSeries != null && cachedSeries.Closes.Count >= minimumCount && DateTime.UtcNow - cachedSeries.Timestamp < TimeSpan.FromHours(2))
            {
                return cachedSeries.Closes.TakeLast(minimumCount).ToList();
            }

            DateTime end = DateTime.UtcNow;
            DateTime start = end.AddDays(-(minimumCount + 50));
            var bars = await _provider.GetHistoricalBarsAsync(symbol, start, end, cancellationToken).ConfigureAwait(false)
                       ?? Array.Empty<Bar>();
            var cleanedBars = await _dataCleaner.CleanAsync(symbol, bars, cancellationToken).ConfigureAwait(false);
            var closes = cleanedBars.Select(b => b.Close).ToList();

            _cache[symbol] = new CachedSeries(DateTime.UtcNow, closes);
            return closes.TakeLast(minimumCount).ToList();
        }

        public void Clear() => _cache.Clear();

        private CachedSeries? GetCachedSeries(string symbol)
        {
            return _cache.TryGetValue(symbol, out var series) ? series : null;
        }

        private record CachedSeries(DateTime Timestamp, IReadOnlyList<double> Closes);
    }
}
