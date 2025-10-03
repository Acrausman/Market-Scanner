using Flurl.Http;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MarketScanner.Data.Providers
{
    public class PolygonMarketDataProvider : IMarketDataProvider
    {
        private readonly string _apiKey;

        public PolygonMarketDataProvider(string apiKey)
        {
            _apiKey = apiKey;
        }

        /// <summary>
        /// Returns the most recent *previous close* price and volume (works on Starter plan).
        /// </summary>
        public async Task<(double price, double volume)> GetQuoteAsync(string symbol)
        {
            // Use today's date for intraday 1-minute bars
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

            var url =
                $"https://api.polygon.io/v2/aggs/ticker/{symbol}/range/1/minute/{today}/{today}?limit=1&sort=desc&apiKey={_apiKey}";

            var response = await url.GetJsonAsync<JObject>();

            var result = response["results"]?.FirstOrDefault();
            if (result == null)
                return (double.NaN, double.NaN);

            double price = result.Value<double?>("c") ?? double.NaN; // close price of last bar
            double volume = result.Value<double?>("v") ?? double.NaN; // volume of last bar

            return (price, volume);
        }


        /// <summary>
        /// Returns a list of recent daily close prices (Starter-compatible).
        /// </summary>
        public async Task<IReadOnlyList<double>> GetHistoricalClosesAsync(string symbol, int days)
        {
            // Polygon’s "range" endpoint
            var to = DateTime.UtcNow.Date;
            var from = to.AddDays(-days * 3); // ask for a wider window (accounts for weekends/holidays)

            string url =
                $"https://api.polygon.io/v2/aggs/ticker/{symbol}/range/1/day/{from:yyyy-MM-dd}/{to:yyyy-MM-dd}?adjusted=true&sort=asc&limit=5000&apiKey={_apiKey}";

            var response = await url.GetJsonAsync<JObject>();

            var results = response["results"]?.ToList();
            if (results == null || results.Count == 0)
                return Array.Empty<double>();

            // Ensure we take exactly the last `days` closes
            return results
                .Select(r => r.Value<double?>("c"))
                .Where(c => c.HasValue)
                .Select(c => c!.Value)
                .TakeLast(days)
                .ToList();
        }

    }
}
