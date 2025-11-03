using System;
using System.Collections.Generic;
using System.Linq;

namespace MarketScanner.Data.Services.Indicators
{
    public static class RsiCalculator
    {
        // ✅ Single-value RSI (used for comparisons or last-bar RSI)
        public static double Calculate(IReadOnlyList<double> closes, int period = 14)
        {
            if (closes == null || closes.Count <= period)
                return double.NaN;

            // find first window with movement
            int start = 1;
            while (start < closes.Count && closes[start] == closes[start - 1])
                start++;

            if (start + period >= closes.Count)
                return 50; // too flat / insufficient data

            double gain = 0, loss = 0;
            for (int i = start; i < start + period; i++)
            {
                double diff = closes[i] - closes[i - 1];
                if (diff >= 0) gain += diff;
                else loss -= diff;
            }

            gain /= period;
            loss /= period;

            double rsi = 50;
            for (int i = start + period; i < closes.Count; i++)
            {
                double diff = closes[i] - closes[i - 1];
                double g = diff > 0 ? diff : 0;
                double l = diff < 0 ? -diff : 0;
                gain = (gain * (period - 1) + g) / period;
                loss = (loss * (period - 1) + l) / period;

                // neutralization for flat segments
                if (gain == 0 && loss == 0) rsi = 50;
                else if (loss == 0) rsi = 100;
                else if (gain == 0) rsi = 0;
                else
                {
                    double rs = gain / loss;
                    rsi = 100 - (100 / (1 + rs));
                }
            }

            return rsi;
        }

        // ✅ Full-series RSI (used for charting)
        public static List<double> CalculateSeries(List<double> closes, int period = 14)
        {
            var rsiValues = new List<double>();
            if (closes == null || closes.Count < period + 1)
                return rsiValues;

            // find first meaningful window
            int start = 1;
            while (start < closes.Count && closes[start] == closes[start - 1])
                start++;

            if (start + period >= closes.Count)
                return Enumerable.Repeat(50.0, closes.Count).ToList();

            double gain = 0, loss = 0;
            for (int i = start; i < start + period; i++)
            {
                double diff = closes[i] - closes[i - 1];
                if (diff >= 0) gain += diff;
                else loss -= diff;
            }

            gain /= period;
            loss /= period;

            double rsi = 50;
            for (int i = 0; i < start + period; i++)
                rsiValues.Add(double.NaN); // prefill for alignment

            for (int i = start + period; i < closes.Count; i++)
            {
                double diff = closes[i] - closes[i - 1];
                double g = diff > 0 ? diff : 0;
                double l = diff < 0 ? -diff : 0;
                gain = (gain * (period - 1) + g) / period;
                loss = (loss * (period - 1) + l) / period;

                if (gain == 0 && loss == 0) rsi = 50;
                else if (loss == 0) rsi = 100;
                else if (gain == 0) rsi = 0;
                else
                {
                    double rs = gain / loss;
                    rsi = 100 - (100 / (1 + rs));
                }

                rsiValues.Add(rsi);
            }

            return rsiValues;
        }
    }
}
