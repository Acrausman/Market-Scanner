using System;
using System.Collections.Generic;
using System.Linq;

namespace MarketScanner.Data.Services.Indicators
{
    public static class RsiCalculator
    {
        /// <summary>
        /// Calculates RSI over exactly the last `period` completed trading sessions.
        /// Uses Wilder's smoothing and skips older history.
        /// </summary>
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
    }
}
