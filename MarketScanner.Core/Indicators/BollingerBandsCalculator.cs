using System;
using System.Collections.Generic;
using System.Linq;

namespace MarketScanner.Core.Indicators
{
    public static class BollingerBandsCalculator
    {
        public static (double Middle, double Upper, double Lower) Calculate(IReadOnlyList<double> values, int period, double multiplier = 2)
        {
            if (values == null || values.Count < period)
            {
                return (double.NaN, double.NaN, double.NaN);
            }

            var window = values.Skip(values.Count - period).Take(period).ToList();
            double sma = window.Average();
            double stdDev = CalculateStandardDeviation(window, sma);
            double upper = sma + multiplier * stdDev;
            double lower = sma - multiplier * stdDev;
            return (sma, upper, lower);
        }

        public static IReadOnlyList<(double Middle, double Upper, double Lower)> CalculateSeries(IReadOnlyList<double> values, int period, double multiplier = 2)
        {
            var series = new List<(double Middle, double Upper, double Lower)>();
            if (values == null || values.Count < period)
            {
                return series;
            }

            for (int i = period; i <= values.Count; i++)
            {
                var window = values.Skip(i - period).Take(period).ToList();
                double sma = window.Average();
                double stdDev = CalculateStandardDeviation(window, sma);
                double upper = sma + multiplier * stdDev;
                double lower = sma - multiplier * stdDev;
                series.Add((sma, upper, lower));
            }

            return series;
        }

        private static double CalculateStandardDeviation(IReadOnlyList<double> values, double mean)
        {
            if (values.Count == 0)
            {
                return double.NaN;
            }

            double variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Count;
            return Math.Sqrt(variance);
        }
    }
}
