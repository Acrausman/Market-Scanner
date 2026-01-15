using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketScanner.Core.Classification
{
    public sealed record CreeperCriteriaR2(
        int SmaPeriod,
        int SlopeLookback,
        double MinSlopePct,
        
        double MaxBollingerWidthPct,
        
        int VolatilityLookback,
        double MaxReturnStdDev,
        
        double MinRsi,
        double MaxRsi,
        
        CreeperTrendDirection Direction);

    public enum CreeperTrendDirection
    {
        Up,
        Down,
        Both
    }
}
