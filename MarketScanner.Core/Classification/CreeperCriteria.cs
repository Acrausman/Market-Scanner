using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketScanner.Core.Classification
{
    public sealed record CreeperCriteria
    {
        public int LookBackBars { get; init; }

        public int BaselinePeriod { get; init; }
        public double MinBarsAboveBaselinePct { get; init; }
        public double MaxBaselineDeviationPct { get; init; }

        public int AtrPeriod { get; init; }
        public double MaxAtrPctOfPrice { get; init; }
        public double AtrCompressionRation { get; init; }

        public double MaxPullbackPct {get; init; }
        public int MaxConsecutiveDownBars { get; init; }
        public int PullbackRecoveryBars { get; init; }

        public double MaxReturnStdDev { get; init; }
        public double MaxGapPct { get; init; }

        public int ScoreThreshold { get; init; }
        public bool StrictMode { get; init; }
    }
}
