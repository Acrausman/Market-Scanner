using MarketScanner.Core.Indicators;
using MarketScanner.Core.Models;
using MathNet.Numerics.Statistics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketScanner.Core.Classification
{
    public class CreeperSignalsR2
    {
        public static double ComputeSmaSlope(
            IReadOnlyList<double> closes,
            int period,
            int lookback)
        {
            var series = SmaCalculator.CalculateSeries(closes, period);
            if (series.Count < lookback + 1)
                return double.NaN;
            double start = series[^(lookback + 1)];
            if (start == 0)
                return double.NaN;
            double end = series[^1];

            return (end - start) / start;
        }
        public static double ComputeBollingerWidthPct(
            double upper,
            double lower,
            double price)
        {
            if (price <= 0 || double.IsNaN(upper) || double.IsNaN(lower))
                return double.NaN;
            return (upper - lower) / price;
        }
        public static double ComputeReturnStdDev(
            IReadOnlyList<Bar> bars,
            int lookback)
        {
            if (bars == null || bars.Count < lookback + 1)
                return double.NaN;

            var window = bars.TakeLast(lookback + 1).ToList();

            var returns = new List<double>(lookback);
            for (int i = 1; i < window.Count; i++)
            {
                double prev = window[i - 1].Close;
                if (prev <= 0)
                    continue;

                returns.Add((window[i].Close - prev) / prev);
            }

            return returns.Count == 0
                ? double.NaN
                : Statistics.StandardDeviation(returns);
        }

        public static bool IsCreeperRsi(double rsi)
        {
            return
                (rsi >= 50 && rsi <= 65) ||
                (rsi >= 35 && rsi < 50);
        }
    }
}
