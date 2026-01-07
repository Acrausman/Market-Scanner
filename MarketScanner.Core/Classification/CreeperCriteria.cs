using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketScanner.Core.Classification
{
    public record CreeperCriteria(
        int LookBackBars,

        int BaselinePeriod,
        double MinBarsAboveBaselinePct,
        double MaxBaselineDeviationPct,

        int AtrPeriod,
        double MaxAtrPctOfPrice,
        double AtrCompressionRatio,

        double MaxPullbackPct,
        int MaxConsecutiveDownBars,
        int PullbackRecoveryBars,

        double MaxReturnStdDev,
        double MaxGapPct,

        double ScoreThreshold,
        bool StrictMode
    );
}