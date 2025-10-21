using Flurl.Http;
using MarketScanner.Data.Providers;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Documents;

public class PolygonMarketDataProvider : IMarketDataProvider
{
    private readonly string _apiKey;
    private readonly bool _useAdjusted = false;

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
            var bars = await GetHistoricalBarsAsync(symbol, 30, _useAdjusted);
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
    public async Task<List<double>> GetHistoricalClosesAsync(string symbol, int limit = 50, bool adjusted = true)
    {
        var bars = await GetHistoricalBarsAsync(symbol, limit, _useAdjusted);
        return bars.Select(b => b.Close).ToList();
    }


    /// <summary>
    /// Gets historical timestamps for the symbol.
    /// </summary>
    public async Task<List<DateTime>> GetHistoricalTimestampsAsync(string symbol, int limit = 50, bool adjusted = true)
    {
        var bars = await GetHistoricalBarsAsync(symbol, limit, _useAdjusted);
        return bars.Select(b => b.Timestamp).ToList();
    }

    /// <summary>
    /// Internal method to fetch historical 15-minute bars from Polygon.
    /// </summary>

    public async Task<List<Bar>> GetHistoricalBarsAsync(string symbol, int limit = 50, bool adjusted = true)
    {
        // ask for a little extra to have room to drop today's partial bar
        int fetch = Math.Max(limit + 80, 120);

        var to = DateTime.UtcNow;
        var from = to.AddDays(-fetch * 2); // overshoot to cover weekends/holidays

        string fromStr = from.ToString("yyyy-MM-dd");
        string toStr = to.ToString("yyyy-MM-dd");

        // ✅ adjusted=true ; daily ; ascending
        /*string url = $"https://api.polygon.io/v2/aggs/ticker/{symbol}/range/1/day/{fromStr}/{toStr}" +
                     $"?adjusted=true&sort=asc&limit={fetch}&apiKey={_apiKey}";
        */
        string url = $"https://api.polygon.io/v2/aggs/ticker/CFSB/range/1/day/2025-06-01/2025-07-30?adjusted={(adjusted? "true" : "false")}&sort=asc&limit=120&apiKey={_apiKey}";

        Console.WriteLine($"[Polygon] Fetching {(adjusted ? "adjusted" : "raw")} bars for {symbol}");
        try
        {
            var response = await url.GetJsonAsync<JObject>();
            var results = response["results"]?.ToList();
            if (results == null || results.Count == 0)
                return new List<Bar>();

            var bars = results.Select(r =>
            {
                long ms = r.Value<long>("t"); // unix ms UTC
                var tsUtc = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
                double close = r.Value<double>("c");
                double volume = r.Value<double>("v");

                return new Bar
                {
                    Timestamp = tsUtc,
                    Close = close,
                    Volume = volume
                };
            })

            .Where(b => !double.IsNaN(b.Close) && b.Close > 0)
            .OrderBy(b => b.Timestamp)
            .ToList();

            // 🚫 Filter out obviously invalid or delisted data
            if (bars.Count > 0 && bars.Average(b => b.Close) < 2)
            {
                Console.WriteLine($"[Polygon] Skipping {symbol} — likely stale or invalid data (avg={bars.Average(b => b.Close):F2})");
                return new List<Bar>();
            }

            // Get ET time safely (Windows/Linux compatible)
            // ----- ensure only completed daily bars, no “today” partials -----
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            var todayEt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date;
            var lastEt = TimeZoneInfo.ConvertTimeFromUtc(bars[^1].Timestamp, tz).Date;
            var marketClosed = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).TimeOfDay >= new TimeSpan(16, 0, 0);

            if (lastEt == todayEt && !marketClosed)
            {
                Console.WriteLine($"[Polygon] Skipping in-progress bar for {symbol}");
                bars.RemoveAt(bars.Count - 1);
            }


            // Finally trim to the requested 'limit'
            if (bars.Count > limit)
                bars = bars.Skip(Math.Max(0, bars.Count - limit)).ToList();

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
        string? nextUrl = $"https://api.polygon.io/v3/reference/tickers" +
                          $"?market=stocks&active=true&type=CS&limit=1000&apiKey={_apiKey}";

        try
        {
            while (!string.IsNullOrEmpty(nextUrl))
            {
                var response = await nextUrl.GetJsonAsync<JObject>();
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

                        // ✅ Relax filter if Polygon doesn’t return exchange/primary flags
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
                if (!string.IsNullOrEmpty(nextUrl))
                    nextUrl += $"&apiKey={_apiKey}";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Polygon] Failed to fetch tickers: {ex.Message}");
        }

        tickers = tickers.Distinct().ToList();
        Console.WriteLine($"[Polygon] Loaded {tickers.Count} common-stock tickers.");
        return tickers;
    }

    public async Task CompareAdjustedAsync(string symbol)
    {
        var adj = await GetHistoricalBarsAsync(symbol, 30, adjusted: true);
        var raw = await GetHistoricalBarsAsync(symbol, 30, adjusted: false);

        Console.WriteLine($"[Compare] {symbol}  adjusted={adj.Last().Close}  raw={raw.Last().Close}");

        for (int i = 0; i < Math.Min(adj.Count, raw.Count); i++)
        {
            double a = adj[i].Close;
            double b = raw[i].Close;
            if (Math.Abs(a - b) > 0.001)
                Console.WriteLine($"  ⚠️  {adj[i].Timestamp:yyyy-MM-dd}: adjusted={a}, raw={b}");
        }
    }


    public async Task DebugTickerInfo(string symbol)
    {
        string url = $"https://api.polygon.io/v3/reference/tickers/{symbol}?apiKey={_apiKey}";
        var response = await url.GetJsonAsync<JObject>();
        Console.WriteLine($"[Debug Info] {symbol}: {response["results"]?["name"]} | " +
                          $"{response["results"]?["market"]} | " +
                          $"{response["results"]?["locale"]} | " +
                          $"{response["results"]?["active"]}");
    }


    private class HistoricalBar
    {
        public DateTime Timestamp { get; set; }
        public double Close { get; set; }
        public double Volume { get; set; }
    }
}
