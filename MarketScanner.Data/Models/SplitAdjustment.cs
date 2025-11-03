using System;

namespace MarketScanner.Data.Models
{
    public class SplitAdjustment
    {
        public DateTime EffectiveDate { get; set; }
        public double AdjustmentFactor { get; set; }
        public string Source { get; set; } = string.Empty;
    }
}
