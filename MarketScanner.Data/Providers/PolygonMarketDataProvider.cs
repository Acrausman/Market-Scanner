using Flurl.Http;
using MarketScanner.Data.Providers;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class PolygonMarketDataProvider : IMarketDataProvider
{
    private readonly string _apiKey;

    public PolygonMarketDataProvider(string apiKey)
    {
        _apiKey = apiKey;
    }

    /// <summary>
    /// Gets the latest 15-minute delayed price and volume for a symbol.
    /// </summary>
    public async Task<(double price, double volume)> GetQuoteAsync(string symbol)
    {
        try
        {
            // Get the most recent 15-minute bar
            var bars = await GetHistoricalBarsAsync(symbol, 1);
            if (bars.Count > 0)
            {
                var lastBar = bars.Last();
                return (lastBar.Close, lastBar.Volume);
            }

            return (double.NaN, double.NaN);
        }
        catch
        {
            return (double.NaN, double.NaN);
        }
    }

    /// <summary>
    /// Gets historical close prices for the symbol.
    /// </summary>
    public async Task<List<double>> GetHistoricalClosesAsync(string symbol, int limit = 50)
    {
        var bars = await GetHistoricalBarsAsync(symbol, limit);
        return bars.Select(b => b.Close).ToList();
    }

    /// <summary>
    /// Gets historical timestamps for the symbol.
    /// </summary>
    public async Task<List<DateTime>> GetHistoricalTimestampsAsync(string symbol, int limit = 50)
    {
        var bars = await GetHistoricalBarsAsync(symbol, limit);
        return bars.Select(b => b.Timestamp).ToList();
    }

    /// <summary>
    /// Internal method to fetch historical 15-minute bars from Polygon.
    /// </summary>
    public async Task<List<Bar>> GetHistoricalBarsAsync(string symbol, int limit = 50)
    {
        var to = DateTime.UtcNow;
        var from = to.AddDays(-limit * 10); // slightly wider range to ensure enough bars

        string fromStr = from.ToString("yyyy-MM-dd");
        string toStr = to.ToString("yyyy-MM-dd");

        string url = $"https://api.polygon.io/v2/aggs/ticker/{symbol}/range/1/day/{fromStr}/{toStr}?sort=asc&limit={limit}&apiKey={_apiKey}";

        try
        {
            var response = await url.GetJsonAsync<JObject>();
            var results = response["results"]?.ToList();

            if (results == null || results.Count == 0)
            {
                Console.WriteLine($"[Polygon] No historical bars for {symbol}");
                return new List<Bar>();
            }

            var bars = results.Select(r =>
            {
                long ms = r.Value<long>("t"); // timestamp in milliseconds
                DateTime timestamp = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
                double close = r.Value<double>("c");
                double volume = r.Value<double>("v");

                return new Bar
                {
                    Close = close,
                    Volume = volume,
                    Timestamp = timestamp
                };
            }).ToList();

            return bars;
        }
        catch (FlurlHttpException ex)
        {
            Console.WriteLine($"[Polygon] Historical bars failed for {symbol}: {ex.Message}");
            return new List<Bar>();
        }
    }

    public async Task<List<string>> GetAllTickersAsync()
    {
        var tickers = new List<string>();
        string? nextUrl = "https://api.polygon.io/v3/reference/tickers?market=stocks&active=true&limit=1000&apiKey=" +_apiKey;

        try
        {
            while (!string.IsNullOrEmpty(nextUrl))
            {
                var response = await nextUrl.GetJsonAsync<JObject>();
                var results = response["results"]?.ToList();

                if (results != null)
                {
                    tickers.AddRange(
                        results
                            .Where(r => r.Value<string>("type") == "CS") // only common stock
                            .Select(r => r.Value<string>("ticker"))
                            .Where(t => !string.IsNullOrEmpty(t))
                            .Select(t => t!)
                    );


                }

                nextUrl = response["next_url"]?.ToString();
                if (!string.IsNullOrEmpty(nextUrl))
                    nextUrl += $"&apiKey={_apiKey}";
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine($"[Polygon] Failed to fetch tickers: {ex.Message}");
        }

        return tickers;
    }



    private class HistoricalBar
    {
        public DateTime Timestamp { get; set; }
        public double Close { get; set; }
        public double Volume { get; set; }
    }
}
