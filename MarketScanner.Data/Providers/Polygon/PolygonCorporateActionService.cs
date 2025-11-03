using MarketScanner.Data.Diagnostics;
using MarketScanner.Data.Models;
using MarketScanner.Data.Providers;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MarketScanner.Data.Providers.Polygon
{
    internal class PolygonCorporateActionService
    {
        private readonly PolygonRestClient _client;

        public PolygonCorporateActionService(PolygonRestClient client)
        {
            _client = client;
        }

        public async Task<IReadOnlyList<SplitAdjustment>> GetSplitAdjustmentsAsync(string symbol, CancellationToken cancellationToken)
        {
            var adjustments = new List<SplitAdjustment>();
            string url = $"https://api.polygon.io/v3/reference/splits?ticker={symbol}";
            try
            {
                var response = await _client.GetJsonAsync(url, cancellationToken).ConfigureAwait(false);
                var results = response["results"]?.ToList() ?? new List<JToken>();
                foreach (var split in results)
                {
                    DateTime date = DateTime.Parse(split.Value<string>("execution_date"), CultureInfo.InvariantCulture);
                    double toFactor = split.Value<double?>("tofactor") ?? 1d;
                    double forFactor = split.Value<double?>("forfactor") ?? 1d;
                    double ratio = forFactor == 0 ? 1d : toFactor / forFactor;
                    adjustments.Add(new SplitAdjustment
                    {
                        EffectiveDate = date,
                        AdjustmentFactor = ratio == 0 ? 1d : 1d / ratio,
                        Source = "Split"
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"[Polygon] {symbol} adjustment failed: {ex.Message}");
            }

            return adjustments;
        }

        public async Task ApplyAdjustmentsAsync(string symbol, IList<Bar> bars, CancellationToken cancellationToken)
        {
            if (bars.Count == 0)
                return;

            var splitAdjustments = await GetSplitAdjustmentsAsync(symbol, cancellationToken).ConfigureAwait(false);
            var dividendAdjustments = await GetDividendAdjustmentsAsync(symbol, bars, cancellationToken).ConfigureAwait(false);

            var adjustments = splitAdjustments.Concat(dividendAdjustments)
                                              .OrderBy(a => a.EffectiveDate)
                                              .ToList();

            foreach (var adjustment in adjustments)
            {
                foreach (var bar in bars.Where(b => b.Timestamp < adjustment.EffectiveDate))
                {
                    bar.Close *= adjustment.AdjustmentFactor;
                }
            }

            if (adjustments.Count > 0)
            {
                Logger.Info($"[Polygon] {symbol}: applied {adjustments.Count} corporate actions");
            }
        }

        private async Task<IReadOnlyList<SplitAdjustment>> GetDividendAdjustmentsAsync(string symbol, IList<Bar> bars, CancellationToken cancellationToken)
        {
            var adjustments = new List<SplitAdjustment>();
            string url = $"https://api.polygon.io/v3/reference/dividends?ticker={symbol}";

            try
            {
                var response = await _client.GetJsonAsync(url, cancellationToken).ConfigureAwait(false);
                var results = response["results"]?.ToList() ?? new List<JToken>();
                foreach (var dividend in results)
                {
                    DateTime date = DateTime.Parse(dividend.Value<string>("ex_dividend_date"), CultureInfo.InvariantCulture);
                    double amount = dividend.Value<double?>("cash_amount") ?? 0d;
                    int exIndex = 0;
                    for (; exIndex < bars.Count; exIndex++)
                    {
                        if (bars[exIndex].Timestamp.Date >= date.Date)
                        {
                            break;
                        }
                    }
                    if (exIndex > 0 && exIndex < bars.Count)
                    {
                        double priorClose = bars[exIndex - 1].Close;
                        if (priorClose > 0)
                        {
                            double factor = (priorClose - amount) / priorClose;
                            adjustments.Add(new SplitAdjustment
                            {
                                EffectiveDate = date,
                                AdjustmentFactor = factor,
                                Source = "Dividend"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"[Polygon] {symbol} adjustment failed: {ex.Message}");
            }

            return adjustments;
        }
    }
}
