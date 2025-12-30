using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketScanner.Core.Models
{
    public sealed record CreeperMetrics
    {
        // Trend
        public double PctAboveBaseline { get; init; }
        public double MaxBaselineDeviationPct { get; init; }
        public double BaselineSlopePct { get; init; }

        // Volatility
        public double AtrPctOfPrice { get; init; }
        public double AtrCompressionRatio { get; init; }

        // Pullbacks
        public double MaxDropdownPct { get; init; }
        public int MaxConsecutiveBars { get; init; }
        public int WorstRecoveryBars { get; init; }

        // Scores
        public double TrendScore { get; init; }
        public double VolatilityScore { get; init; }
        public double PullbackScore { get; init; }
    }
}
