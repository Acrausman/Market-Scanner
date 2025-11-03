#if DEBUG
using MarketScanner.Data.Providers;
using MarketScanner.Data.Services.Indicators;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace MarketScanner.Data.Diagnostics
{
    public static class RsiCompareStooqHelper
    {
        private static readonly HttpClient _http = new HttpClient();
        private static readonly string _cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StooqCache");

        static RsiCompareStooqHelper()
        {
            Directory.CreateDirectory(_cacheDir);
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("MarketScanner-Debug/1.0");
        }

        public static async Task CompareAllAsync(PolygonMarketDataProvider provider, params string[] symbols)
        {
            Logger.Info($"\n==== RSI COMPARISON TEST (Stooq) {DateTime.UtcNow:yyyy-MM-dd} ====\n");
            foreach (var sym in symbols)
            {
                await CompareAsync(provider, sym);
                await Task.Delay(1500); // polite delay
            }
            Logger.Info("\n==== DONE ====\n");
        }

        public static async Task CompareAsync(PolygonMarketDataProvider provider, string symbol, int period = 14)
        {
            try
            {
                var end = DateTime.UtcNow;
                var start = end.AddDays(-200);
                var bars = await provider.GetHistoricalBarsAsync(symbol, start, end).ConfigureAwait(false);
                var myCloses = bars.OrderBy(b => b.Timestamp).Select(b => b.Close).ToList();
                if (myCloses.Count == 0)
                {
                    Logger.Warn($"[RSI Compare] {symbol}: no provider bars.");
                    return;
                }

                var myRsi = RsiCalculator.Calculate(myCloses, period);

                // Download Stooq CSV
                var stooqSymbol = NormalizeToStooqSymbol(symbol);
                var cacheFile = Path.Combine(_cacheDir, $"{stooqSymbol}.csv");

                string csv;
                if (File.Exists(cacheFile) &&
                    DateTime.UtcNow - File.GetLastWriteTimeUtc(cacheFile) < TimeSpan.FromHours(24))
                {
                    csv = await File.ReadAllTextAsync(cacheFile);
                    Logger.Debug($"[Stooq] Using cached CSV for {stooqSymbol}");
                }
                else
                {
                    var url = $"https://stooq.com/q/d/l/?s={stooqSymbol}&i=d";
                    Logger.Info($"[Stooq] Downloading CSV for {stooqSymbol}...");
                    csv = await _http.GetStringAsync(url);
                    await File.WriteAllTextAsync(cacheFile, csv);
                }

                var stooqCloses = ParseStooqCloses(csv);
                if (stooqCloses.Count == 0)
                {
                    Logger.Warn($"[Stooq] No rows parsed for {stooqSymbol}");
                    return;
                }

                // Normalize scale
                double ratio = myCloses.Last() / stooqCloses.Last();
                stooqCloses = stooqCloses.Select(c => c * ratio).ToList();
                Logger.Debug($"[Normalize] {symbol}: scaled Stooq closes by ratio {ratio:F3}");

                // Align series length and drop incomplete bar
                int count = Math.Min(myCloses.Count, stooqCloses.Count);
                myCloses = myCloses.Skip(myCloses.Count - count).ToList();
                stooqCloses = stooqCloses.Skip(stooqCloses.Count - count).ToList();
                if (myCloses.Count > 0) myCloses.RemoveAt(myCloses.Count - 1);
                if (stooqCloses.Count > 0) stooqCloses.RemoveAt(stooqCloses.Count - 1);

                double stooqRsi = RsiCalculator.Calculate(stooqCloses, period);

                Logger.Info($"\n[RSI Compare] {symbol} (Stooq={stooqSymbol})");
                Logger.Info($"  Provider RSI = {myRsi:F2}");
                Logger.Info($"  Stooq    RSI = {stooqRsi:F2}");
                Logger.Info($"  δ = {Math.Abs(myRsi - stooqRsi):F2} points\n");

                Logger.Info("  Recent closes (Provider vs Stooq):");
                for (int i = 0; i < 5 && i < myCloses.Count && i < stooqCloses.Count; i++)
                {
                    Logger.Info($"   {i + 1,2}: Provider={myCloses[^(i + 1)],8:F2} | Stooq={stooqCloses[^(i + 1)],8:F2}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[RSI Compare] {symbol}: failed — {ex.Message}");
            }
        }

        private static string NormalizeToStooqSymbol(string symbol)
        {
            var s = symbol.Trim().ToLowerInvariant();
            return s.Contains('.') ? s : $"{s}.us";
        }

        private static List<double> ParseStooqCloses(string csv)
        {
            var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length <= 1) return new List<double>();

            var closes = new List<double>(lines.Length - 1);
            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Trim().Split(',');
                if (parts.Length >= 5 &&
                    double.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out double close))
                {
                    closes.Add(close);
                }
            }
            return closes;
        }
    }
}
#endif
