using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace MarketScanner.Data.Providers
{
    public class PolygonMarketDataProvider : IMarketDataProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public PolygonMarketDataProvider(string apiKey)
        {
            _httpClient = new HttpClient();
            _apiKey = apiKey;
        }

        /// <summary>
        /// Gets the latest price + volume from Polygon.
        /// Priority: Snapshot → Last Trade → Aggregates.
        /// </summary>
        public async Task<(double price, double volume)> GetQuoteAsync(string symbol)
        {
            // --- 1. Snapshot API ---
            try
            {
                var url = $"https://api.polygon.io/v2/snapshot/locale/us/markets/stocks/tickers/{symbol}?apiKey={_apiKey}";
                var json = await _httpClient.GetStringAsync(url);

                var token = JObject.Parse(json)?["ticker"];
                if (token != null)
                {
                    double price = token?["lastTrade"]?["p"]?.Value<double?>() ?? double.NaN;
                    double volume = token?["day"]?["v"]?.Value<double?>() ?? double.NaN;

                    if (!double.IsNaN(price))
                        return (price, volume);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Polygon] Snapshot failed for {symbol}: {ex.Message}");
            }

            // --- 2. Last Trade API ---
            try
            {
                var url = $"https://api.polygon.io/v2/last/trade/{symbol}?apiKey={_apiKey}";
                var json = await _httpClient.GetStringAsync(url);

                var token = JObject.Parse(json)?["results"];
                if (token != null)
                {
                    double price = token?["p"]?.Value<double?>() ?? double.NaN;
                    if (!double.IsNaN(price))
                        return (price, double.NaN); // no volume available here
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Polygon] Last Trade failed for {symbol}: {ex.Message}");
            }

            // --- 3. Aggregates API fallback ---
            try
            {
                var from = DateTime.UtcNow.AddDays(-2);
                var to = DateTime.UtcNow;

                var url =
                    $"https://api.polygon.io/v2/aggs/ticker/{symbol}/range/1/day/{from:yyyy-MM-dd}/{to:yyyy-MM-dd}?apiKey={_apiKey}";
                var json = await _httpClient.GetStringAsync(url);

                var results = JObject.Parse(json)?["results"] as JArray;
                if (results != null && results.Count > 0)
                {
                    var last = results.Last;
                    double price = last?["c"]?.Value<double?>() ?? double.NaN;
                    double volume = last?["v"]?.Value<double?>() ?? double.NaN;
                    return (price, volume);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Polygon] Aggregate fallback failed for {symbol}: {ex.Message}");
            }

            return (double.NaN, double.NaN);
        }

        /// <summary>
        /// Gets historical daily closes for SMA/RSI calculations.
        /// </summary>
        public async Task<IReadOnlyList<double>> GetHistoricalClosesAsync(string symbol, int days)
        {
            try
            {
                var from = DateTime.UtcNow.AddDays(-days);
                var to = DateTime.UtcNow;

                var url =
                    $"https://api.polygon.io/v2/aggs/ticker/{symbol}/range/1/day/{from:yyyy-MM-dd}/{to:yyyy-MM-dd}?apiKey={_apiKey}";
                var json = await _httpClient.GetStringAsync(url);

                var results = JObject.Parse(json)?["results"] as JArray;
                if (results == null || results.Count == 0)
                    return new List<double>();

                return results
                    .Select(r => r?["c"]?.Value<double?>() ?? double.NaN)
                    .Where(v => !double.IsNaN(v))
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Polygon] Error in GetHistoricalClosesAsync({symbol}): {ex.Message}");
                return new List<double>();
            }
        }
    }
}
