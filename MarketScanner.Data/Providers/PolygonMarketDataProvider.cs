using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using MarketScanner.Data.Providers;

namespace MarketScanner.Data.Providers
{
    public class PolygonMarketDataProvider : IMarketDataProvider
    {
        private readonly string _apiKey;
        private readonly HttpClient _http;

        public PolygonMarketDataProvider(string apiKey)
        {
            _apiKey = apiKey;
            _http = new HttpClient { BaseAddress = new Uri("https://api.polygon.io") };
        }

        public async Task<(double price, double volume)> GetQuoteAsync(string symbol)
        {
            try
            {
                var url = $"/v2/aggs/ticker/{symbol}/prev?apiKey={_apiKey}";
                var json = await _http.GetStringAsync(url);
                var obj = JObject.Parse(json);

                var result = obj["results"]?.FirstOrDefault();
                if (result == null)
                    throw new Exception($"No results found for {symbol}");

                double price = result["c"]?.Value<double>() ?? 0;  // close price
                double volume = result["v"]?.Value<double>() ?? 0; // volume

                return (price, volume);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Polygon] GetQuoteAsync failed for {symbol}: {ex.Message}");
                return (double.NaN, double.NaN);
            }
        }

        public async Task<IReadOnlyList<double>> GetHistoricalClosesAsync(string symbol, int days)
        {
            try
            {
                var to = DateTime.UtcNow.Date;
                var from = to.AddDays(-days);

                var url = $"/v2/aggs/ticker/{symbol}/range/1/day/{from:yyyy-MM-dd}/{to:yyyy-MM-dd}?apiKey={_apiKey}";
                var json = await _http.GetStringAsync(url);
                var obj = JObject.Parse(json);

                var results = obj["results"]?.ToList();
                if (results == null || results.Count == 0)
                    return Array.Empty<double>();

                return results.Select(r => r["c"]?.Value<double>() ?? 0).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Polygon] GetHistoricalClosesAsync failed for {symbol}: {ex.Message}");
                return Array.Empty<double>();
            }
        }
    }
}
