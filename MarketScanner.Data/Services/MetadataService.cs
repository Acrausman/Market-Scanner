using MarketScanner.Core.Abstractions;
using MarketScanner.Core.Metadata;
using MarketScanner.Core.Models;
using MarketScanner.Data.Diagnostics;
using MarketScanner.Data.Providers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketScanner.Data.Services
{
    public class MetadataService : IMetadataService
    {
        private readonly TickerMetadataCache _cache;
        private readonly IMarketDataProvider _marketDataProvider;
        private readonly IFundamentalProvider _fundamentals;

        public MetadataService(
            TickerMetadataCache cache,
            IMarketDataProvider marketDataProvider,
            IFundamentalProvider fundamentals)
        {
            _cache = cache;
            _marketDataProvider = marketDataProvider;
            _fundamentals = fundamentals;
        }

        public async Task<TickerInfo> EnsureMetadataAsync(
            TickerInfo info,
            CancellationToken token)
        {
            //Logger.WriteLine($"Metadata requested for {info.Symbol}");

            if (!_cache.TryGet(info.Symbol, out var meta) ||
                    string.IsNullOrWhiteSpace(meta?.Sector) ||
                    string.IsNullOrWhiteSpace(meta?.Country))
            {
                try
                {
                    meta = await _fundamentals.GetMetadataAsync(info.Symbol, token);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"[Metadata] Failed to fetch metadata for {info.Symbol}: {ex.Message}");
                }
                if(meta != null)
                {
                    _cache.AddOrUpdate(meta);
                }
            }

            if (meta != null)
            {
                info.Country = meta.Country;
                info.Sector = meta.Sector;
                info.Exchange = meta.Exchange;
            }
            return info;
        }

        public async Task PreloadAsync(IProgress<int>? progress, CancellationToken token)
        {
            var tickers = await _marketDataProvider.GetAllTickersAsync();

            int processed = 0;
            foreach (var t in tickers)
            {
                if(!_cache.TryGet(t.Symbol, out _))
                {
                    try
                    {
                        var meta = await _fundamentals.GetMetadataAsync(t.Symbol, token)
                            .ConfigureAwait(false);
                        if (meta != null)
                            _cache.AddOrUpdate(meta);
                    }
                    catch(Exception ex)
                    {
                        Logger.WriteLine($"[Metadata] Failed to fetch metadata for {t.Symbol}: {ex.Message}");
                    }
                }

                processed++;
                progress?.Report((processed * 100) / tickers.Count);
            }
            _cache.SaveCacheToDisk();
        }
    }
}
