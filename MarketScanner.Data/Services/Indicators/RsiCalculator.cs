using System;
using System.Collections.Generic;
using System.Linq;
using YahooFinanceApi;

namespace MarketScanner.Data.Services.Indicators
{
    public static class RsiCalculator
    {
        public static double Calculate(IReadOnlyList<double> closes, int period = 14,
                                       RsiSmoothingMethod method = RsiSmoothingMethod.Wilder)
        {
            return method switch
            { RsiSmoothingMethod.Simple => CalculateSimple(closes,period),
              RsiSmoothingMethod.Ema => CalculateEma(closes,period),
              _ => CalculateWilder(closes,period)
            };
        }

        private static double CalculateWilder(IReadOnlyList<double> closes, int period = 14)
        {
            for (int i = 1; i < closes.Count; i++)
            {
                var diff = closes[i] - closes[i - 1];
                //Console.WriteLine($"Delta={diff:F4}, close={closes[i]:F2}");
            }

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
        private static double CalculateSimple(IReadOnlyList<double> closes, int period = 14)
        {
            if (closes.Count < period + 1) return double.NaN;

            var diffs = closes.Skip(1).Zip(closes, (curr, prev) => curr - prev).ToList();
            double avgGain = diffs.Where(d => d > 0).TakeLast(period).DefaultIfEmpty(0).Average();
            double avgLoss = diffs.Where(d => d < 0).Select(d => -d).TakeLast(period).DefaultIfEmpty(0).Average();

            double rs = avgLoss == 0 ? double.PositiveInfinity : avgGain / avgLoss;
            return 100 - (100 / (1 + rs));
        }
        private static double CalculateEma(IReadOnlyList<double> closes, int period = 14)
        {
            if (closes.Count < period + 1) return double.NaN;

            var gains = new List<double>();
            var losses = new List<double>();

            for (int i = 1; i < closes.Count; i++)
            {
                double diff = closes[i] - closes[i - 1];
                gains.Add(Math.Max(diff, 0));
                losses.Add(Math.Max(-diff, 0));
            }

            double alpha = 2.0 / (period + 1);
            double avgGain = gains.Take(period).Average();
            double avgLoss = losses.Take(period).Average();

            for (int i = period; i < gains.Count; i++)
            {
                avgGain = alpha * gains[i] + (1 - alpha) * avgGain;
                avgLoss = alpha * losses[i] + (1 - alpha) * avgLoss;
            }

            double rs = avgLoss == 0 ? double.PositiveInfinity : avgGain / avgLoss;
            return 100 - (100 / (1 + rs));
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
