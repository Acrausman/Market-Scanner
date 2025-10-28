using System;
using System.Collections.Generic;
using System.Linq;

namespace MarketScanner.Data.Services.Indicators
{
    public static class RsiCalculator
    {
        public static double Calculate(IReadOnlyList<double> closes, int period = 14)
        {
            if (closes == null || closes.Count <= period)
                return double.NaN;

            double gain = 0, loss = 0;
            for (int i = 1; i <= period; i++)
            {
                double diff = closes[i] - closes[i - 1];
                if (diff > 0) gain += diff;
                else loss -= diff;
            }

            double avgGain = gain / period;
            double avgLoss = loss / period;

            for (int i = period + 1; i < closes.Count; i++)
            {
                double diff = closes[i] - closes[i - 1];
                double up = diff > 0 ? diff : 0;
                double down = diff < 0 ? -diff : 0;

                avgGain = ((avgGain * (period - 1)) + up) / period;
                avgLoss = ((avgLoss * (period - 1)) + down) / period;
            }

            if (avgLoss == 0) return 100;
            double rs = avgGain / avgLoss;
            return Math.Round(100 - (100 / (1 + rs)), 2);
        }
        public static List<double> CalculateSeries(List<double> closes, int period)
        {
            var rsiValues = new List<double>();
            if (closes.Count < period + 1)
                return rsiValues;

            double gain = 0, loss = 0;
            for (int i = 1; i <= period; i++)
            {
                double change = closes[i] - closes[i - 1];
                if (change > 0) gain += change;
                else loss -= change;
            }

            double avgGain = gain / period;
            double avgLoss = loss / period;

            double rs = avgLoss == 0 ? 100 : avgGain / avgLoss;
            rsiValues.Add(100 - (100 / (1 + rs)));

            for (int i = period + 1; i < closes.Count; i++)
            {
                double change = closes[i] - closes[i - 1];
                double gainVal = change > 0 ? change : 0;
                double lossVal = change < 0 ? -change : 0;

                avgGain = ((avgGain * (period - 1)) + gainVal) / period;
                avgLoss = ((avgLoss * (period - 1)) + lossVal) / period;

                rs = avgLoss == 0 ? 100 : avgGain / avgLoss;
                double rsi = 100 - (100 / (1 + rs));
                rsiValues.Add(rsi);
            }

            return rsiValues;
        }

    }
}
