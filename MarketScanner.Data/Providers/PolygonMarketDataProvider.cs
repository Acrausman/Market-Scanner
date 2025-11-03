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
using System.Threading;
using System.Threading.Tasks;
using Twilio.Jwt.AccessToken;

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
            /*string divUrl = $"https://api.polygon.io/v3/reference/dividends?ticker={symbol}&apiKey={_apiKey}";
            var divResp = await divUrl.GetJsonAsync<JObject>();
            var divs = divResp["results"]?.ToList() ?? new List<JToken>();*/

            // Combine both actions chronologically
            var actions = new List<(DateTime Date, double Factor)>();

            // Splits → multiplicative (divide earlier closes)
            foreach (var s in splits)
            {
                DateTime date = DateTime.Parse(s.Value<string>("execution_date"));
                double toF = s.Value<double>("tofactor");
                double forF = s.Value<double>("forfactor");
                double ratio = forF / toF;
                actions.Add((date, ratio)); // multiply earlier prices by 1/ratio
            }

            // Dividends → scaling factor
            /*foreach (var d in divs)
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
            }*/

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

    public async Task<List<double>> GetHistoricalClosesAsync(string symbol, int limit = 50, bool adjusted = true)
    {
        //Console.WriteLine($"[Provider] Fetching closes for {symbol}");
        var bars = await GetHistoricalBarsAsync(symbol, limit);
        //Console.WriteLine($"[Provider] {symbol} -> {bars.Count} bars, last close={bars.LastOrDefault()?.Close:F2}");
        return bars.Select(b => b.Close).ToList();
    }

    public async Task<List<DateTime>> GetHistoricalTimestampsAsync(string symbol, int limit = 50, bool adjusted = true)
    {
        var bars = await GetHistoricalBarsAsync(symbol, limit, _useAdjusted);
        return bars.Select(b => b.Timestamp).ToList();
    }

    public async Task<List<Bar>> GetHistoricalBarsAsync(string symbol, int limit = 50, bool adjusted = true)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var bars = new List<Bar>();

        int fetch = Math.Max(limit + 80, 120);
        var to = DateTime.UtcNow;
        var from = to.AddDays(-fetch * 2);

        string fromStr = from.ToString("yyyy-MM-dd");
        string toStr = to.ToString("yyyy-MM-dd");

        // ✅ Always request fully adjusted data from Polygon
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

                // Normalize to 4 PM ET (market close)
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
            Logger.WriteLine($"[RSI Debug] {symbol} bar count = {bars.Count}");
            Logger.WriteLine($"[RSI Debug] date span = {(bars.Last().Timestamp - bars.First().Timestamp).TotalDays:F0} days");
            Logger.WriteLine($"[RSI Debug] missing days: {bars.Count - bars.Select(b => b.Timestamp.Date).Distinct().Count()} (duplicates/holes)");

            // 🚫 Do NOT apply manual corporate adjustments here
            Logger.WriteLine($"[Polygon] {symbol}: using adjusted data directly (no manual corrections).");

            // Drop today's partial bar if market not closed
            var nowEt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            if (bars.Count > 0)
            {
                var lastEt = TimeZoneInfo.ConvertTimeFromUtc(bars.Last().Timestamp, tz).Date;
                if (lastEt == nowEt.Date && nowEt.TimeOfDay < new TimeSpan(16, 0, 0))
                    bars.RemoveAt(bars.Count - 1);
            }

            // Trim to requested limit
            if (bars.Count > limit)
                bars = bars.Skip(bars.Count - limit).ToList();
            Logger.WriteLine($"[Polygon] {symbol} sample closes:");
            foreach (var b in bars.TakeLast(5))
                Logger.WriteLine($"  {b.Timestamp:yyyy-MM-dd}  {b.Close:F2}");

<<<<<<< Updated upstream
            Console.WriteLine($"[Polygon] {symbol} -> {bars.Count} bars, last close={bars.Last().Close:F2}");
=======
>>>>>>> Stashed changes
            return bars;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Polygon] {symbol} failed: {ex.Message}");
            return new List<Bar>();
        }
    }

    /// <summary>
    /// Checks if Polygon's "adjusted=true" prices already include split/dividend adjustments.
    /// </summary>
    private bool DetectIfPolygonDataIsAdjusted(List<Bar> bars, List<JToken>? splits)
    {
        if (splits == null || splits.Count == 0 || bars.Count < 2)
            return true; // assume adjusted if no corporate actions or short history

        try
        {
            var earliest = bars.First().Close;
            var latest = bars.Last().Close;
            var mostRecentSplit = splits.OrderByDescending(s => DateTime.Parse(s.Value<string>("execution_date"))).First();
            double toF = mostRecentSplit.Value<double>("tofactor");
            double forF = mostRecentSplit.Value<double>("forfactor");
            double ratio = forF / toF; // correct direction (e.g., 4-for-1 => ratio=4)

            // Heuristic: if latest/earliest ratio already ≈ adjusted, assume Polygon adjusted data
            if (latest / earliest < ratio * 2 && latest / earliest > ratio / 2)
                return true;
        }
        catch { }

        return false;
    }


    public async Task CompareRsiWithYahooAsync(string symbol, int period = 14)
    {
        try
        {
            Logger.WriteLine($"\n[RSI Compare] Starting comparison for {symbol}...");

            // 1️⃣ Get Polygon RSI (adjusted + corporate-action corrected)
            var myBars = await GetHistoricalBarsAsync(symbol, 150);
            var myCloses = myBars.OrderBy(b => b.Timestamp).Select(b => b.Close).ToList();
            if (myCloses.Count < period + 1)
            {
                Logger.WriteLine($"[RSI Compare] {symbol}: insufficient Polygon data ({myCloses.Count} bars).");
                return;
            }

            double myRsi = RsiCalculator.Calculate(myCloses, period);
            var closes = myBars.Select(b => b.Close).ToList();
            for (int i = 0; i < closes.Count; i++)
            {
                if (i > 0)
                {
                    double diff = closes[i] - closes[i - 1];
                    Logger.WriteLine($"  {i,3}: {closes[i - 1]:F2} → {closes[i]:F2}  Δ={diff:+0.00;-0.00}");
                }
            }


            // 2️⃣ Try Yahoo first
            List<double> externalCloses = await FetchYahooClosesAsync(symbol);
            await Task.Delay(Random.Shared.Next(400, 800));

            // 3️⃣ Fallback to Finnhub if Yahoo failed or rate-limited
            if (externalCloses.Count < period + 1)
            {
                Logger.WriteLine($"[RSI Compare] Falling back to Finnhub for {symbol}...");
                externalCloses = await FetchFinnhubClosesAsync(symbol);
            }

            if (externalCloses.Count < period + 1)
            {
                Logger.WriteLine($"[RSI Compare] {symbol}: insufficient external data ({externalCloses.Count} bars).");
                return;
            }

            double extRsi = RsiCalculator.Calculate(externalCloses, period);

            // 4️⃣ Print comparison
            Logger.WriteLine($"\n[RSI Compare] {symbol}");
            Logger.WriteLine($"  Polygon RSI = {myRsi:F2}");
            Logger.WriteLine($"  External RSI = {extRsi:F2}");
            Logger.WriteLine($"  Δ = {Math.Abs(myRsi - extRsi):F2} points");

            Logger.WriteLine("\n  Recent closes (Polygon vs External):");
            for (int i = 0; i < Math.Min(5, myCloses.Count); i++)
            {
                double myC = myCloses[^Math.Min(i + 1, myCloses.Count)];
                double extC = externalCloses[^Math.Min(i + 1, externalCloses.Count)];
                Logger.WriteLine($"   {i + 1,2}: Polygon={myC,8:F2} | External={extC,8:F2}");
            }
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"[RSI Compare] {symbol}: failed - {ex.Message}");
        }
    }

    /// <summary>
    /// Yahoo JSON endpoint (preferred, but rate-limited).
    /// </summary>
    private async Task<List<double>> FetchYahooClosesAsync(string symbol)
    {
        try
        {
            string url = $"https://query1.finance.yahoo.com/v8/finance/chart/{symbol}?interval=1d&range=6mo";
            using var http = new HttpClient();

            var resp = await http.GetAsync(url);
            if ((int)resp.StatusCode == 429)
            {
                Logger.WriteLine($"[Yahoo] 429 Too Many Requests — throttled for {symbol}");
                return new List<double>();
            }

            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            var j = JObject.Parse(json);
            var closes = j["chart"]?["result"]?[0]?["indicators"]?["quote"]?[0]?["close"]
                ?.Select(t => t.Type == JTokenType.Null ? double.NaN : t.Value<double>())
                .Where(v => !double.IsNaN(v))
                .ToList();

            return closes ?? new List<double>();
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"[Yahoo] Failed to fetch {symbol}: {ex.Message}");
            return new List<double>();
        }
    }

    /// <summary>
    /// Finnhub fallback — requires free API key (10–60 req/min depending on plan).
    /// </summary>
    private async Task<List<double>> FetchFinnhubClosesAsync(string symbol)
    {
        try
        {
            string apiKey = "d44drfhr01qt371uia8gd44drfhr01qt371uia90";
            string finnhubSymbol = symbol.Contains(":") ? symbol : symbol;
            string url = $"https://finnhub.io/api/v1/stock/candle?symbol={finnhubSymbol}&resolution=D&count=180&token={apiKey}";
            Logger.WriteLine($"[Finnhub] Requesting: {url}");

            var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = System.Net.DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            };

            using var http = new HttpClient(handler);

            // 🧠 Mimic a real Chrome browser request
            http.DefaultRequestHeaders.Clear();
            http.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                "AppleWebKit/537.36 (KHTML, like Gecko) " +
                "Chrome/120.0.0.0 Safari/537.36");
            http.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
            http.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
            http.DefaultRequestHeaders.Add("Referer", "https://finnhub.io/");

            var resp = await http.GetAsync(url);
            // Randomized short delay to prevent 429 rate-limit from Yahoo
            await Task.Delay(Random.Shared.Next(400, 800));
            Logger.WriteLine($"[Finnhub] HTTP {(int)resp.StatusCode} - {resp.ReasonPhrase} for {finnhubSymbol}");

            if (!resp.IsSuccessStatusCode)
            {
                var msg = await resp.Content.ReadAsStringAsync();
                Logger.WriteLine($"[Finnhub] Body: {msg}");
                return new List<double>();
            }

            var json = await resp.Content.ReadAsStringAsync();
            var j = JObject.Parse(json);

            if (j["s"]?.ToString() != "ok")
            {
                Logger.WriteLine($"[Finnhub] No valid data for {finnhubSymbol}. Status={j["s"]}");
                return new List<double>();
            }

            var closes = j["c"]?.Select(t => t.Value<double>()).ToList();
            return closes ?? new List<double>();
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"[Finnhub] Failed to fetch {symbol}: {ex.Message}");
            return new List<double>();
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

                        // Relax filter if Polygon doesn’t return exchange/primary flags
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
