using MarketScanner.Data.Diagnostics;
using MarketScanner.Core.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MarketScanner.Data.Providers.Polygon
{
    internal class PolygonBarDownloader
    {
        private readonly PolygonRestClient _client;
        private readonly TimeZoneInfo _easternTimeZone;

        public PolygonBarDownloader(PolygonRestClient client)
        {
            _client = client;
            _easternTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }

        public async Task<List<Bar>> FetchDailyBarsAsync(string symbol, DateTime start, DateTime end, CancellationToken cancellationToken, bool adjusted = true)
        {
            var paddedStart = start.AddDays(-Math.Max(40, (end - start).TotalDays / 2));
            var bars = await FetchAggregatesAsync(symbol, paddedStart, end, adjusted, cancellationToken).ConfigureAwait(false);
            if (adjusted)
            {
                await OverwriteWithOpenCloseAsync(symbol, bars, cancellationToken).ConfigureAwait(false);
            }
            RemoveIncompleteSession(bars);
            return bars.Where(b => b.Timestamp >= start.ToUniversalTime() && b.Timestamp <= end.ToUniversalTime())
                       .OrderBy(b => b.Timestamp)
                       .ToList();
        }

        private async Task<List<Bar>> FetchAggregatesAsync(string symbol, DateTime start, DateTime end, bool adjusted, CancellationToken cancellationToken)
        {
            var bars = new List<Bar>();
            string url = $"https://api.polygon.io/v2/aggs/ticker/{symbol}/range/1/day/{start:yyyy-MM-dd}/{end:yyyy-MM-dd}?adjusted={(adjusted ? "true" : "false")}&unadjusted={(adjusted ? "false" : "true")}&sort=asc&limit=5000";

            try
            {
                var response = await _client.GetJsonAsync(url, cancellationToken).ConfigureAwait(false);
                var results = response["results"]?.ToList();
                if (results == null || results.Count == 0)
                {
                    return bars;
                }

                foreach (var r in results)
                {
                    long ms = r.Value<long>("t");
                    var tsUtc = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
                    var et = TimeZoneInfo.ConvertTimeFromUtc(tsUtc, _easternTimeZone);
                    if (et.Hour < 9)
                    {
                        et = et.AddDays(-1);
                    }

                    var normalizedUtc = TimeZoneInfo.ConvertTimeToUtc(et.Date.AddHours(16), _easternTimeZone);
                    bars.Add(new Bar
                    {
                        Timestamp = normalizedUtc,
                        Close = r.Value<double>("c"),
                        Volume = r.Value<double?>("v") ?? 0d
                    });
                }

                return bars.OrderBy(b => b.Timestamp).ToList();
            }
            catch (Exception ex)
            {
                //Logger.Error($"[Polygon] {symbol} failed: {ex.Message}");
                return new List<Bar>();
            }
        }

        private async Task OverwriteWithOpenCloseAsync(string symbol, IList<Bar> bars, CancellationToken cancellationToken)
        {
            if (bars.Count == 0)
            {
                return;
            }

            int recentDays = Math.Min(20, bars.Count);
            var recentStartEt = TimeZoneInfo.ConvertTimeFromUtc(bars[^recentDays].Timestamp, _easternTimeZone).Date;

            var tasks = new List<Task<(DateTime date, double close)>>();
            for (int i = 0; i < recentDays; i++)
            {
                var day = recentStartEt.AddDays(i);
                tasks.Add(FetchOfficialCloseAsync(symbol, day, cancellationToken));
            }

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            foreach (var (date, close) in results)
            {
                if (double.IsNaN(close))
                {
                    continue;
                }

                var match = bars.FirstOrDefault(b => TimeZoneInfo.ConvertTimeFromUtc(b.Timestamp, _easternTimeZone).Date == date.Date);
                if (match != null)
                {
                    match.Close = close;
                }
            }
        }

        private async Task<(DateTime date, double close)> FetchOfficialCloseAsync(string symbol, DateTime date, CancellationToken cancellationToken)
        {
            string ocUrl = $"https://api.polygon.io/v1/open-close/{symbol}/{date:yyyy-MM-dd}?adjusted=true";
            try
            {
                var response = await _client.GetJsonAsync(ocUrl, cancellationToken).ConfigureAwait(false);
                if (response == null || response["close"] == null)
                {
                    return (date, double.NaN);
                }

                return (date, response.Value<double>("close"));
            }
            catch
            {
                return (date, double.NaN);
            }
        }

        private void RemoveIncompleteSession(IList<Bar> bars)
        {
            if (bars.Count == 0)
            {
                return;
            }

            var nowEt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _easternTimeZone);
            var lastBarEt = TimeZoneInfo.ConvertTimeFromUtc(bars[^1].Timestamp, _easternTimeZone);
            if (lastBarEt.Date == nowEt.Date && nowEt.TimeOfDay < new TimeSpan(16, 0, 0))
            {
                bars.RemoveAt(bars.Count - 1);
            }
        }
    }
}
