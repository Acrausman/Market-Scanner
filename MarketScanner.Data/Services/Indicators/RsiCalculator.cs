using System;
using System.Collections.Generic;
using System.Linq;

namespace MarketScanner.Data.Services.Indicators
{
    public static class RsiCalculator
    {
        // Classic 14-period RSI using last 15 closes only (Wilder seed)
        public static double Calculate(IReadOnlyList<double> closes, int period = 14)
        {
            if (closes == null) return double.NaN;

            // Clean & ensure chronological order
            var series = closes.Where(c => !double.IsNaN(c) && c > 0).ToList();
            if (series.Count < period + 1) return double.NaN;

            // Use exactly the last (period+1) closes
            var window = series.Skip(series.Count - (period + 1)).ToList();

            double gain = 0, loss = 0;
            for (int i = 1; i <= period; i++)
            {
                double diff = window[i] - window[i - 1];
                if (diff > 0) gain += diff; else loss -= diff;
            }

            double avgGain = gain / period;
            double avgLoss = loss / period;

            if (avgLoss == 0) return 100.0;
            if (avgGain == 0) return 0.0;

            double rs = avgGain / avgLoss;
            return 100.0 - (100.0 / (1.0 + rs));
        }
    }


}
