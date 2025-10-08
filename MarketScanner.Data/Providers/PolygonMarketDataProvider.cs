using Flurl.Http;
using MarketScanner.Data.Providers;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class PolygonMarketDataProvider: IMarketDataProvider
{
    private readonly string _apiKey;

    public PolygonMarketDataProvider(string apiKey)
    {
        _apiKey = apiKey;
    }

    // --- Current quote via /v2/snapshot (works on Starter Plan)
    public async Task<(double price, double volume)> GetQuoteAsync(string symbol)
    {
        var url = $"https://api.polygon.io/v2/snapshot/locale/us/markets/stocks/tickers/{symbol}?apiKey={_apiKey}";

        try
        {
            var response = await url.GetJsonAsync<JObject>();
            var ticker = response["ticker"] ?? response;

            double price = ticker["lastTrade"]?.Value<double?>("p") ?? double.NaN;
            double volume = ticker["day"]?.Value<double?>("v") ?? double.NaN;

            // fallback if snapshot missing last trade
            if (double.IsNaN(price))
            {
                var closes = await GetHistoricalClosesAsync(symbol, 1);
                if (closes.Count > 0)
                    price = closes.Last();
            }
            //Console.WriteLine($"[Polygon] Snapshot {symbol} → Price={price}, Volume={volume}");
            return (price, volume);
        }
        catch (FlurlHttpException ex)
        {
            //Console.WriteLine($"[Polygon] Snapshot failed for {symbol}: {ex.Message}");
            return (double.NaN, double.NaN);
        }
    }


    // --- Historical closes via /v2/aggs (15-min delayed but OK for Starter)
    public async Task<List<double>> GetHistoricalClosesAsync(string symbol, int limit = 50)
    {
        var to = DateTime.UtcNow;
        var from = to.AddDays(-limit * 2); // fetch a slightly wider range to ensure enough candles

        var fromStr = from.ToString("yyyy-MM-dd");
        var toStr = to.ToString("yyyy-MM-dd");

        var url =
            $"https://api.polygon.io/v2/aggs/ticker/{symbol}/range/1/day/{fromStr}/{toStr}?sort=asc&limit={limit}&apiKey={_apiKey}";

        //Console.Write($"[Polygon] Requesting {symbol} closes -> {url}");

        try
        {
            var response = await url.GetJsonAsync<JObject>();

            if (response == null || response.Count == 0)
            {
                //Console.WriteLine($"[Polygon] NULL JSON response for {symbol}");
                return new List<double>();
            }
            if(response.ContainsKey("error"))
            {
                Console.WriteLine($"[Polygon] API error for {symbol} {response["error"]}");
                return new List<double>();
            }
            var results = response["results"]?.ToList();
            if(results == null || results.Count == 0)
            {
                Console.WriteLine($"[Polygon] No historical data for {symbol}");
                Console.WriteLine($"[Polygon] Full JSON: {response.ToString()}");
                return new List<double>();
            }

            var closes = results
                .Select(r => r.Value<double?>("c"))
                .Where(v => v.HasValue)
                .Select(v => v.Value)
                .ToList();

            //Console.WriteLine($"[Polygon] History for {symbol}: Count={closes.Count}");
            return closes;
        }
        catch (FlurlHttpException ex)
        {
            Console.WriteLine($"[Polygon] Historical data failed for {symbol}: {ex.Message}");
            try
            {
                var body = await ex.GetResponseStringAsync();
                //Console.WriteLine($"[Polygon] Error body: {body}");
            }
            catch(Exception exa)
            {
                //Console.WriteLine($"[Polygon] Unexpected error for {symbol}: {exa}");
                return new List<double>();
            }
            return new List<double>();
        }
    }

    public async Task<List<DateTime>> GetHistoricalTimestampsAsync(string symbol, int limit = 50)
    {
        var to = DateTime.UtcNow;
        var from = to.AddDays(-limit * 2);

        var fromStr = from.ToString("yyyy-MM-dd");
        var toStr = to.ToString("yyyy-MM-dd");

        var url = $"https://api.polygon.io/v2/aggs/ticker/{symbol}/range/1/day/{fromStr}/{toStr}?sort=asc&limit={limit}&apiKey={_apiKey}";

        //Console.WriteLine($"[Polygon] Requesting {symbol} timestamps -> {url}");

        try
        {
            var response = await url.GetJsonAsync<JObject>();
            var results = response["results"]?.ToList();
            if (results == null)
            {
                //Console.WriteLine($"[Polygon] No timestamps for symbol");
                return new List<DateTime>();
            }

            var timestamps = results
                .Select(r =>
                {
                    long ms = r.Value<long>("t");
                    return DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime;
                })
                .ToList();

            //Console.WriteLine($"[Polygon] Timestamps for {symbol}: Count={timestamps.Count}");
            return timestamps;
        }
        catch (FlurlHttpException ex)
        {
            //Console.WriteLine($"[Polygon] Historical timestamps failed for {symbol}: {ex.Message}");
            return new List<DateTime>();
        }
    }

}
