// Normalized after refactor: updated namespace and using references
using System;
using System.Collections.Generic;
using System.Linq;

namespace MarketScanner.Core.Indicators
{
    public static class SmaCalculator
    {
        public static double Calculate(IReadOnlyList<double> values, int period)
        {
            if (values == null || period <= 0 || values.Count < period)
                return double.NaN;

            double sum = 0;
            for (int i = values.Count - period; i < values.Count; i++)
                sum += values[i];

            return sum / period;
        }


        public static IReadOnlyList<double> CalculateSeries(
            IReadOnlyList<double> values,
            int period)
        {
            var result = new List<double>();

            if (values == null || period <= 0 || values.Count < period)
                return result;

            for (int i = period - 1; i < values.Count; i++)
            {
                double sum = 0;

                for (int j = i - period + 1; j <= i; j++)
                {
                    sum += values[j];
                }

                result.Add(sum / period);
            }

            return result;
        }
    }
}
