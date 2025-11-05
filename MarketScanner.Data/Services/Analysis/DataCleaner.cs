using MarketScanner.Core.Abstractions;
using MarketScanner.Core.Models;
using MarketScanner.Data.Diagnostics;
using MarketScanner.Data.Models;
using MarketScanner.Data.Providers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MarketScanner.Data.Services.Analysis
{
    public class DataCleaner : IDataCleaner
    {
        private readonly IMarketDataProvider _provider;
        private readonly IAppLogger _logger;

        public DataCleaner(IMarketDataProvider provider, IAppLogger logger)
        {
            _provider = provider;
            _logger = logger;
        }

        public async Task<IReadOnlyList<Bar>> CleanAsync(string symbol, IReadOnlyList<Bar> bars, CancellationToken cancellationToken)
        {
            if (bars == null || bars.Count == 0)
            {
                return Array.Empty<Bar>();
            }

            cancellationToken.ThrowIfCancellationRequested();

            var ordered = bars
                .Where(b => b != null && !double.IsNaN(b.Close))
                .Select(b => new Bar
                {
                    Close = b.Close,
                    Volume = b.Volume,
                    Timestamp = b.Timestamp
                })
                .OrderBy(b => b.Timestamp)
                .ToList();

            if (ordered.Count == 0)
            {
                return ordered;
            }

            RemoveDuplicatesByTimestamp(ordered);

            var adjustments = await _provider
                .GetSplitAdjustmentsAsync(symbol, cancellationToken)
                .ConfigureAwait(false);

            if (adjustments != null && adjustments.Count > 0)
            {
                ApplySplitAdjustments(ordered, adjustments);
                //_logger.Log(LogSeverity.Debug, $"[DataCleaner] Applied {adjustments.Count} split adjustments for {symbol}.");
            }

            return ordered;
        }

        private static void RemoveDuplicatesByTimestamp(IList<Bar> bars)
        {
            if (bars.Count <= 1)
            {
                return;
            }

            var unique = new Collection<Bar>();
            foreach (var bar in bars)
            {
                if (unique.Count == 0 || unique[^1].Timestamp != bar.Timestamp)
                {
                    unique.Add(bar);
                }
                else
                {
                    unique[^1] = bar;
                }
            }

            if (unique.Count != bars.Count)
            {
                bars.Clear();
                foreach (var bar in unique)
                {
                    bars.Add(bar);
                }
            }
        }

        private static void ApplySplitAdjustments(IList<Bar> bars, IReadOnlyList<SplitAdjustment> adjustments)
        {
            var orderedAdjustments = adjustments
                .Where(a => a != null && a.AdjustmentFactor > 0)
                .OrderBy(a => a.EffectiveDate)
                .ToList();

            foreach (var adjustment in orderedAdjustments)
            {
                for (int i = 0; i < bars.Count; i++)
                {
                    if (bars[i].Timestamp >= adjustment.EffectiveDate)
                    {
                        continue;
                    }

                    bars[i].Close *= adjustment.AdjustmentFactor;
                    bars[i].Volume /= adjustment.AdjustmentFactor;
                }
            }
        }
    }
}
