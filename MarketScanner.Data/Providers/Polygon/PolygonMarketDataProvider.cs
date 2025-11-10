using MarketScanner.Data.Diagnostics;
using MarketScanner.Core.Models;
using MarketScanner.Data.Models;
using MarketScanner.Data.Providers.Polygon;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MarketScanner.Data.Providers
{
    public class PolygonMarketDataProvider : IMarketDataProvider
    {
        private readonly PolygonRestClient _client;
        private readonly PolygonBarDownloader _barDownloader;
        private readonly PolygonCorporateActionService _corporateActionService;

        public PolygonMarketDataProvider(string apiKey)
        {
            _client = new PolygonRestClient(apiKey);
            _barDownloader = new PolygonBarDownloader(_client);
            _corporateActionService = new PolygonCorporateActionService(_client);
        }

        public async Task<(double price, double volume)> GetQuoteAsync(string symbol, CancellationToken cancellationToken = default)
        {
            var end = DateTime.UtcNow;
            var start = end.AddDays(-30);
            var bars = await GetHistoricalBarsAsync(symbol, start, end, cancellationToken).ConfigureAwait(false);
            var lastBar = bars.LastOrDefault();
            if (lastBar == null)
            {
                return (double.NaN, double.NaN);
            }

            return (lastBar.Close, lastBar.Volume);
        }

        public async Task<IReadOnlyList<Bar>> GetHistoricalBarsAsync(string symbol, DateTime start, DateTime end, CancellationToken cancellationToken = default)
        {
            var bars = await _barDownloader.FetchDailyBarsAsync(symbol, start, end, cancellationToken).ConfigureAwait(false);
            await _corporateActionService.ApplyAdjustmentsAsync(symbol, bars, cancellationToken).ConfigureAwait(false);
            return bars;
        }

        public Task<IReadOnlyList<SplitAdjustment>> GetSplitAdjustmentsAsync(string symbol, CancellationToken cancellationToken = default)
        {
            return _corporateActionService.GetSplitAdjustmentsAsync(symbol, cancellationToken);
        }

        public async Task<IReadOnlyList<string>> GetAllTickersAsync(CancellationToken cancellationToken = default)
        {
            var tickers = new List<string>();
            string? nextUrl = "https://api.polygon.io/v3/reference/tickers?market=stocks&active=true&type=CS&limit=1000";

            try
            {
                while (!string.IsNullOrEmpty(nextUrl))
                {
                    var response = await _client.GetJsonAsync(nextUrl, cancellationToken).ConfigureAwait(false);
                    var results = response["results"]?.ToList();
                    if (results != null)
                    {
                        foreach (var r in results)
                        {
                            string? type = r.Value<string>("type");
                            string? exchange = r.Value<string>("primary_exchange");
                            bool? active = r.Value<bool?>("active");
                            bool? primary = r.Value<bool?>("primary_share");
                            string? ticker = r.Value<string>("ticker");

                            if (!string.IsNullOrWhiteSpace(ticker) &&
                                type == "CS" &&
                                (active ?? true) &&
                                (primary == null || primary == true) &&
                                (exchange != null && new[] { "XNYS", "XNAS", "XASE" }.Contains(exchange)))
                            {
                                tickers.Add(ticker);
                            }
                        }
                    }

                    nextUrl = response["next_url"]?.ToString();
                }
            }
            catch (Exception ex)
            {
                //Logger.Warn($"[Polygon] Failed to fetch tickers: {ex.Message}");
            }

            return tickers.Distinct().ToList();
        }

        public async Task<IReadOnlyList<TickerInfo>> GetAllTickerInfoAsync(CancellationToken cancellationToken = default)
        {
            var tickers = new List<TickerInfo>();
            string? nextUrl = "https://api.polygon.io/v3/reference/tickers?market=stocks&active=true&type=CS&limit=1000";

            try
            {
                while (!string.IsNullOrEmpty(nextUrl))
                {
                    var response = await _client.GetJsonAsync(nextUrl, cancellationToken).ConfigureAwait(false);
                    var results = response["results"]?.ToList();

                    if (results != null)
                    {
                        foreach (var r in results)
                        {
                            string? ticker = r.Value<string>("ticker");
                            string? type = r.Value<string>("type");
                            string? exchange = r.Value<string>("primary_exchange");
                            bool? active = r.Value<bool?>("active");
                            bool? primary = r.Value<bool?>("primary_share");
                            string? locale = r.Value<string>("locale");
                            string? sector = r.Value<string>("sic_description") ?? r.Value<string>("sector");

                            if (!string.IsNullOrWhiteSpace(ticker) &&
                                type == "CS" &&
                                (active ?? true) &&
                                (primary == null || primary == true) &&
                                (exchange != null && new[] { "XNYS", "XNAS", "XASE" }.Contains(exchange)))
                            {
                                tickers.Add(new TickerInfo
                                {
                                    Symbol = ticker,
                                    Country = locale?.ToUpperInvariant() ?? "US",
                                    Sector = sector ?? "Unknown"
                                });
                            }
                        }
                    }

                    nextUrl = response["next_url"]?.ToString();
                }
            }
            catch (Exception ex)
            {
                //Logger.Warn($"[Polygon] Failed to fetch tickers: {ex.Message}");
            }

            return tickers;
        }

    }
}
