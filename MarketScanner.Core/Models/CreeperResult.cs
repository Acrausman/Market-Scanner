using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketScanner.Core.Models
{
    public class CreeperResult
    {
        public bool IsCreeper { get; init; }
        public int Score { get; init; }
        public CreeperType Type { get; init; }

        public IReadOnlyDictionary<string, double>? Metrics { get; init; }

        public static CreeperResult NotCreeper() =>
            new() { IsCreeper = false, Score = 0 };
    }
}
