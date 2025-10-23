using Flurl.Http;
using MarketScanner.Data.Diagnostics;
using MarketScanner.Data.Providers;
using MarketScanner.Data.Services.Indicators;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

public class PolygonMarketDataProvider : IMarketDataProvider
{
    private readonly string _apiKey;
    private readonly bool _useAdjusted = false;

    public PolygonMarketDataProvider(string apiKey)
    {
        _apiKey = apiKey;
    }
    private async Task ApplyCorporateActionsAsync(string symbol, List<Bar> bars)
    {
        try
        {
            // --- Fetch splits ---
            string splitUrl = $"https://api.polygon.io/v3/reference/splits?ticker={symbol}&apiKey={_apiKey}";
            var splitResp = await splitUrl.GetJsonAsync<JObject>();
            var splits = splitResp["results"]?.ToList() ?? new List<JToken>();

            // --- Fetch dividends ---
            string divUrl = $"https://api.polygon.io/v3/reference/dividends?ticker={symbol}&apiKey={_apiKey}";
            var divResp = await divUrl.GetJsonAsync<JObject>();
            var divs = divResp["results"]?.ToList() ?? new List<JToken>();

            // Combine both actions chronologically
            var actions = new List<(DateTime Date, double Factor)>();

            // Splits → multiplicative (divide earlier closes)
            foreach (var s in splits)
            {
                DateTime date = DateTime.Parse(s.Value<string>("execution_date"));
                double toF = s.Value<double>("tofactor");
                double forF = s.Value<double>("forfactor");
                double ratio = toF / forF;
                actions.Add((date, 1 / ratio)); // multiply earlier prices by 1/ratio
            }

            // Dividends → scaling factor
            foreach (var d in divs)
            {
                DateTime date = DateTime.Parse(d.Value<string>("ex_dividend_date"));
                double amount = d.Value<double>("cash_amount");
                int exIndex = bars.FindIndex(b => b.Timestamp.Date >= date.Date);
                if (exIndex > 0 && exIndex < bars.Count)
                {
                    double priorClose = bars[exIndex - 1].Close;
                    double factor = (priorClose - amount) / priorClose;
                    actions.Add((date, factor));
                }
            }

            // Sort and apply in chronological order
            foreach (var act in actions.OrderBy(a => a.Date))
            {
                foreach (var b in bars.Where(b => b.Timestamp < act.Date))
                    b.Close *= act.Factor;
            }

            if (actions.Count > 0)
                Logger.WriteLine($"[Polygon] {symbol}: applied {actions.Count} corporate actions");
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"[Polygon] {symbol} adjustment failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the latest 15-minute delayed price and volume for a symbol.
    /// </summary>
    public async Task<(double price, double volume)> GetQuoteAsync(string symbol)
    {
        try
        {
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
        //Console.WriteLine($"[Provider] Fetching closes for {symbol}");
        var bars = await GetHistoricalBarsAsync(symbol, limit);
        //Console.WriteLine($"[Provider] {symbol} -> {bars.Count} bars, last close={bars.LastOrDefault()?.Close:F2}");
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
    /// Fetches historical daily bars for a given symbol.
    /// This version aligns Polygon's UTC bars to Eastern time market closes.
    /// </summary>

    public async Task<List<Bar>> GetHistoricalBarsAsync(string symbol, int limit = 50, bool adjusted = true)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var bars = new List<Bar>();

        // 1️⃣ Fetch bulk data quickly via v2/aggs
        int fetch = Math.Max(limit + 80, 120);
        var to = DateTime.UtcNow;
        var from = to.AddDays(-fetch * 2);

        string fromStr = from.ToString("yyyy-MM-dd");
        string toStr = to.ToString("yyyy-MM-dd");

        string url = $"https://api.polygon.io/v2/aggs/ticker/{symbol}/range/1/day/{fromStr}/{toStr}" +
                     $"?adjusted=true&sort=asc&limit={fetch}&apiKey={_apiKey}";

        try
        {
            var response = await url.GetJsonAsync<JObject>();
            var results = response["results"]?.ToList();
            if (results == null || results.Count == 0)
                return new List<Bar>();

            foreach (var r in results)
            {
                long ms = r.Value<long>("t");
                var tsUtc = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
                var et = TimeZoneInfo.ConvertTimeFromUtc(tsUtc, tz);
                if (et.Hour < 9) et = et.AddDays(-1);
                var normalizedUtc = TimeZoneInfo.ConvertTimeToUtc(et.Date.AddHours(16), tz);

                bars.Add(new Bar
                {
                    Timestamp = normalizedUtc,
                    Close = r.Value<double>("c"),
                    Volume = r.Value<double>("v")
                });
            }

            bars = bars.OrderBy(b => b.Timestamp).ToList();

            // 2️⃣ Fetch accurate closes for last ~20 days from v1/open-close
            int recentDays = 20;
            var recentStartEt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date.AddDays(-recentDays);

            var recentTasks = new List<Task<(DateTime date, double close)>>();

            for (int i = 0; i < recentDays; i++)
            {
                var d = recentStartEt.AddDays(i);
                string dateStr = d.ToString("yyyy-MM-dd");
                string ocUrl = $"https://api.polygon.io/v1/open-close/{symbol}/{dateStr}?adjusted=true&apiKey={_apiKey}";
                recentTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var resp = await ocUrl.GetJsonAsync<JObject>();
                        if (resp == null || resp["close"] == null) return (d, double.NaN);
                        return (d, resp.Value<double>("close"));
                    }
                    catch { return (d, double.NaN); }
                }));
            }

            var ocResults = await Task.WhenAll(recentTasks);
            foreach (var (date, close) in ocResults)
            {
                if (double.IsNaN(close)) continue;
                var match = bars.FirstOrDefault(b =>
                    TimeZoneInfo.ConvertTimeFromUtc(b.Timestamp, tz).Date == date);
                if (match != null)
                    match.Close = close; // overwrite with official close
            }

            // 3️⃣ Drop today's partial bar if market not closed
            var nowEt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            if (bars.Count > 0)
            {
                var lastEt = TimeZoneInfo.ConvertTimeFromUtc(bars.Last().Timestamp, tz).Date;
                if (lastEt == nowEt.Date && nowEt.TimeOfDay < new TimeSpan(16, 0, 0))
                    bars.RemoveAt(bars.Count - 1);
            }

            // 4️⃣ Trim to requested limit
            if (bars.Count > limit)
                bars = bars.Skip(bars.Count - limit).ToList();

            Console.WriteLine($"[Polygon] {symbol} -> {bars.Count} bars, last close={bars.Last().Close:F2}");
            return bars;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Polygon] {symbol} failed: {ex.Message}");
            return new List<Bar>();
        }
    }


    public async Task CompareRsiWithYahooAsync(string symbol, int period = 14)
{
    try
    {
        // 1️⃣  Get your own RSI
        var myBars = await GetHistoricalBarsAsync(symbol, 150);
        var myCloses = myBars.OrderBy(b => b.Timestamp).Select(b => b.Close).ToList();
        double myRsi = RsiCalculator.Calculate(myCloses, period);

        // 2️⃣  Fetch Yahoo closes for same symbol (past ~6 months)
        var end = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var start = end - (180 * 24 * 3600);
        string yahooUrl = $"https://query1.finance.yahoo.com/v7/finance/download/{symbol}?period1={start}&period2={end}&interval=1d&events=history";

        using var http = new HttpClient();
        var csv = await http.GetStringAsync(yahooUrl);

        var yahooCloses = new List<double>();
        foreach (var line in csv.Split('\n').Skip(1))
        {
            var parts = line.Split(',');
            if (parts.Length >= 5 && double.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out double close))
                yahooCloses.Add(close);
        }

        double yahooRsi = RsiCalculator.Calculate(yahooCloses, period);

        // 3️⃣  Show results
        Logger.WriteLine($"\n[RSI Compare] {symbol}");
        Logger.WriteLine($"  Polygon RSI = {myRsi:F2}");
        Logger.WriteLine($"  Yahoo   RSI = {yahooRsi:F2}");
        Logger.WriteLine($"  Δ = {Math.Abs(myRsi - yahooRsi):F2} points");

        Logger.WriteLine("\n  Recent closes (Polygon vs Yahoo):");
        for (int i = 0; i < 5; i++)
        {
            double myC = myCloses[^Math.Min(i + 1, myCloses.Count)];
            double yhC = yahooCloses[^Math.Min(i + 1, yahooCloses.Count)];
            Logger.WriteLine($"   {i + 1,2}: Polygon={myC,8:F2} | Yahoo={yhC,8:F2}");
        }
    }
    catch (Exception ex)
    {
        Logger.WriteLine($"[RSI Compare] {symbol}: failed - {ex.Message}");
    }
}

public async Task CheckBarAlignmentAsync(string symbol)
    {
        var bars = await GetHistoricalBarsAsync(symbol, 20);

        var tz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        Logger.WriteLine($"\n[TimeCheck] {symbol} — Showing 10 latest bars (UTC vs ET):\n");

        foreach (var b in bars.TakeLast(10))
        {
            var et = TimeZoneInfo.ConvertTimeFromUtc(b.Timestamp, tz);
            Logger.WriteLine($"UTC: {b.Timestamp:yyyy-MM-dd HH:mm}Z  |  ET: {et:yyyy-MM-dd HH:mm}  |  Close={b.Close}");
        }
    }

    // --- The rest of your original methods unchanged (GetAllTickersAsync, CompareAdjustedAsync, DebugTickerInfo) ---

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
            //Console.WriteLine($"[Polygon] Failed to fetch tickers: {ex.Message}");
        }

        tickers = tickers.Distinct().ToList();
        //Console.WriteLine($"[Polygon] Loaded {tickers.Count} common-stock tickers.");
        return tickers;
    }

    public async Task CompareAdjustedAsync(string symbol)
    {
        var adj = await GetHistoricalBarsAsync(symbol, 30, adjusted: true);
        var raw = await GetHistoricalBarsAsync(symbol, 30, adjusted: false);

        Logger.WriteLine($"[Compare] {symbol}  adjusted={adj.Last().Close}  raw={raw.Last().Close}");

        for (int i = 0; i < Math.Min(adj.Count, raw.Count); i++)
        {
            double a = adj[i].Close;
            double b = raw[i].Close;
            if (Math.Abs(a - b) > 0.001)
                Logger.WriteLine($"  ⚠️  {adj[i].Timestamp:yyyy-MM-dd}: adjusted={a}, raw={b}");
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
