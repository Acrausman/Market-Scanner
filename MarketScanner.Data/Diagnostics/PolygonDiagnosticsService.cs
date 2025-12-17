// Normalized after refactor: updated namespace and using references
using Flurl;
using Flurl.Http;
using MarketScanner.Core.Models;
using MarketScanner.Core.Configuration;
using MarketScanner.Data.Models;
using MarketScanner.Data.Providers;
using MarketScanner.Data.Providers.Polygon;
using MarketScanner.Core.Indicators;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MarketScanner.Data.Diagnostics
{
    public class PolygonDiagnosticsService
    {
        private readonly AppSettings _settings;
        private readonly PolygonMarketDataProvider _provider;
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;

        public PolygonDiagnosticsService(PolygonMarketDataProvider provider, string apiKey, AppSettings settings)
        {
            _settings = settings;
            _provider = provider;
            _apiKey = apiKey;
            _httpClient = new HttpClient();
        }

        public async Task CompareRsiWithYahooAsync(string symbol, int period = 14, CancellationToken cancellationToken = default)
        {
            try
            {
                var end = DateTime.UtcNow;
                var start = end.AddDays(-200);
                var myBars = await _provider.GetHistoricalBarsAsync(symbol, start, end, cancellationToken).ConfigureAwait(false);
                var myCloses = myBars.OrderBy(b => b.Timestamp).Select(b => b.Close).ToList();
                double myRsi = RsiCalculator.Calculate(myCloses, period, _settings.RsiMethod);

                var endUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var startUnix = endUnix - (180 * 24 * 3600);
                string yahooUrl = $"https://query1.finance.yahoo.com/v7/finance/download/{symbol}?period1={startUnix}&period2={endUnix}&interval=1d&events=history";
                var csv = await _httpClient.GetStringAsync(yahooUrl, cancellationToken).ConfigureAwait(false);

                var yahooCloses = new List<double>();
                foreach (var line in csv.Split('\n').Skip(1))
                {
                    var parts = line.Split(',');
                    if (parts.Length >= 5 && double.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out double close))
                    {
                        yahooCloses.Add(close);
                    }
                }

                double yahooRsi = RsiCalculator.Calculate(yahooCloses, period, _settings.RsiMethod);

                Logger.Info($"\n[RSI Compare] {symbol}");
                Logger.Info($"  Polygon RSI = {myRsi:F2}");
                Logger.Info($"  Yahoo   RSI = {yahooRsi:F2}");
                Logger.Info($"  Δ = {Math.Abs(myRsi - yahooRsi):F2} points");

                Logger.Info("\n  Recent closes (Polygon vs Yahoo):");
                for (int i = 0; i < 5; i++)
                {
                    double myC = myCloses[^Math.Min(i + 1, myCloses.Count)];
                    double yhC = yahooCloses[^Math.Min(i + 1, yahooCloses.Count)];
                    Logger.Info($"   {i + 1,2}: Polygon={myC,8:F2} | Yahoo={yhC,8:F2}");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"[RSI Compare] {symbol}: failed - {ex.Message}");
            }
        }

        public async Task CheckBarAlignmentAsync(string symbol, CancellationToken cancellationToken = default)
        {
            var end = DateTime.UtcNow;
            var start = end.AddDays(-40);
            var bars = await _provider.GetHistoricalBarsAsync(symbol, start, end, cancellationToken).ConfigureAwait(false);

            var tz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            Logger.Info($"\n[TimeCheck] {symbol} — Showing 10 latest bars (UTC vs ET):\n");

            foreach (var bar in bars.TakeLast(10))
            {
                var et = TimeZoneInfo.ConvertTimeFromUtc(bar.Timestamp, tz);
                Logger.Info($"UTC: {bar.Timestamp:yyyy-MM-dd HH:mm}Z  |  ET: {et:yyyy-MM-dd HH:mm}  |  Close={bar.Close}");
            }
        }

        public async Task CompareAdjustedAsync(string symbol, CancellationToken cancellationToken = default)
        {
            var end = DateTime.UtcNow;
            var start = end.AddDays(-60);

            var adjusted = (await _provider.GetHistoricalBarsAsync(symbol, start, end, cancellationToken).ConfigureAwait(false)).ToList();
            var raw = await new PolygonBarDownloader(new PolygonRestClient(_apiKey))
                .FetchDailyBarsAsync(symbol, start, end, cancellationToken, adjusted: false).ConfigureAwait(false);

            Logger.Info($"[Compare] {symbol}  adjusted={adjusted.Last().Close}  raw={raw.Last().Close}");

            for (int i = 0; i < Math.Min(adjusted.Count, raw.Count); i++)
            {
                double a = adjusted[i].Close;
                double b = raw[i].Close;
                if (Math.Abs(a - b) > 0.001)
                {
                    Logger.Info($"  ⚠️  {adjusted[i].Timestamp:yyyy-MM-dd}: adjusted={a}, raw={b}");
                }
            }
        }

        public async Task DebugTickerInfo(string symbol, CancellationToken cancellationToken = default)
        {
            string url = $"https://api.polygon.io/v3/reference/tickers/{symbol}";
            var request = new Flurl.Url(url).SetQueryParam("apiKey", _apiKey);
            var response = await request.GetJsonAsync<JObject>(cancellationToken).ConfigureAwait(false);
            Logger.Info($"[Debug Info] {symbol}: {response["results"]?["name"]} | {response["results"]?["market"]} | {response["results"]?["locale"]} | {response["results"]?["active"]}");
        }
    }
}
