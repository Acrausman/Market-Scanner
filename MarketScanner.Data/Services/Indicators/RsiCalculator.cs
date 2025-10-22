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

            // ✅ Only consider the most recent `period + 1` closes (15 points for RSI 14)
            var recent = closes.Skip(Math.Max(0, closes.Count - (period + 1))).ToList();

            double gain = 0, loss = 0;

            // Initial averages from first 'period' differences
            for (int i = 1; i < recent.Count; i++)
            {
                double diff = recent[i] - recent[i - 1];
                if (diff >= 0) gain += diff;
                else loss -= diff;
            }

            double avgGain = gain / period;
            double avgLoss = loss / period;

            // ⚙️ Handle edge cases
            if (avgLoss == 0)
                return avgGain == 0 ? 50 : 100;

            double rs = avgGain / avgLoss;
            double rsi = 100 - (100 / (1 + rs));

            return Math.Round(rsi, 2);
        }
    }
}
