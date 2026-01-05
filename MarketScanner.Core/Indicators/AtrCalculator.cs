using System;
using System.Collections.Generic;
using System.Linq;
using MarketScanner.Core.Models;

namespace MarketScanner.Core.Indicators
{
    public static class AtrCalculator
    {
        public static double Calculate(
            IReadOnlyList<Bar> bars,
            int period)
        {
            if(bars == null || bars.Count < period + 1)
                return double.NaN;

            double sum = 0;

            var trueRanges = new List<double>();

            for(int i = bars.Count - period; i < bars.Count; i++)
            {
                var current = bars[i];
                var previous = bars[i - 1];

                double tr = Math.Max(
                    current.High - current.Low,
                    Math.Max(
                        Math.Abs(current.High - previous.Close),
                        Math.Abs(current.Low - previous.Close)));
                sum += tr;
            }

            return sum / period;
        }

        public static IReadOnlyList<double> CalculateSeries(
            IReadOnlyList<Bar> bars,
            int period)
        {
            var result = new List<double>();

            if (bars == null || bars.Count < period + 1)
                return result;

            for (int i = period + 1; i <= bars.Count; i++)
            {
                var window = bars
                    .Skip(i - (period + 1))
                    .Take(period + 1)
                    .ToArray();

                double atr = Calculate(window, period);
                result.Add(atr);
            }
            return result;
        }

        private sealed record VolatilityMetrics
            (
                double AtrPctOfPrice,
                double AtrCompressionRatio
            );
    }
}
