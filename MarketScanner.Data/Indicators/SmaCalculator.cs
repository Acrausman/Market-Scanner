// Normalized after refactor: updated namespace and using references
using System;
using System.Collections.Generic;
using System.Linq;

namespace MarketScanner.Data.Indicators
{
    public static class SmaCalculator
    {
        public static double Calculate(IReadOnlyList<double> values, int period)
        {
            if (values == null || values.Count < period)
            {
                return double.NaN;
            }

            return values.Skip(values.Count - period).Average();
        }

        public static IReadOnlyList<double> CalculateSeries(IReadOnlyList<double> values, int period)
        {
            var result = new List<double>();
            if (values == null || values.Count < period)
            {
                return result;
            }

            for (int i = period; i <= values.Count; i++)
            {
                result.Add(values.Skip(i - period).Take(period).Average());
            }

            return result;
        }
    }
}
