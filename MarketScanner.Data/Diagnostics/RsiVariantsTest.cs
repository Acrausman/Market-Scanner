// Normalized after refactor: updated namespace and using references
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MarketScanner.Data.Diagnostics
{
    public static class RsiVariantsTest
    {
        public static void Run()
        {
            // Example data from Wilder's original book
            var closes = new List<double>
            {
                44.34, 44.09, 44.15, 43.61, 44.33, 44.83, 45.10,
                45.42, 45.84, 46.08, 45.89, 46.03, 45.61, 46.28, 46.28
            };
            int period = 14;

            Console.WriteLine("=== RSI Variant Comparison ===");
            Console.WriteLine($"Period: {period}");
            Console.WriteLine($"Closes: {string.Join(", ", closes.Select(c => c.ToString("F2", CultureInfo.InvariantCulture)))}");
            Console.WriteLine();

            double rsiWilder = CalculateWilder(closes, period);
            double rsiSimple = CalculateSimple(closes, period);
            double rsiEMA = CalculateEMA(closes, period);

            Console.WriteLine($"Wilder RSI: {rsiWilder:F2}  ← (expected ≈ 70.46)");
            Console.WriteLine($"Simple RSI: {rsiSimple:F2}");
            Console.WriteLine($"EMA RSI:    {rsiEMA:F2}");
        }

        // --- 1️⃣ Wilder's original RSI (what your project currently uses)
        private static double CalculateWilder(IReadOnlyList<double> closes, int period)
        {
            if (closes.Count < period + 1) return double.NaN;

            double gain = 0, loss = 0;
            for (int i = 1; i <= period; i++)
            {
                double change = closes[i] - closes[i - 1];
                if (change > 0) gain += change;
                else loss -= change;
            }

            gain /= period;
            loss /= period;

            double rs = loss == 0 ? double.PositiveInfinity : gain / loss;
            double rsi = 100 - (100 / (1 + rs));

            for (int i = period + 1; i < closes.Count; i++)
            {
                double change = closes[i] - closes[i - 1];
                double g = change > 0 ? change : 0;
                double l = change < 0 ? -change : 0;
                gain = (gain * (period - 1) + g) / period;
                loss = (loss * (period - 1) + l) / period;

                rs = loss == 0 ? double.PositiveInfinity : gain / loss;
                rsi = 100 - (100 / (1 + rs));
            }
            return rsi;
        }

        // --- 2️⃣ Simple RSI (mean of last N gains/losses)
        private static double CalculateSimple(IReadOnlyList<double> closes, int period)
        {
            if (closes.Count < period + 1) return double.NaN;

            var diffs = closes.Skip(1).Zip(closes, (curr, prev) => curr - prev).ToList();
            double avgGain = diffs.Where(d => d > 0).TakeLast(period).DefaultIfEmpty(0).Average();
            double avgLoss = diffs.Where(d => d < 0).Select(d => -d).TakeLast(period).DefaultIfEmpty(0).Average();

            double rs = avgLoss == 0 ? double.PositiveInfinity : avgGain / avgLoss;
            return 100 - (100 / (1 + rs));
        }

        // --- 3️⃣ EMA-smoothed RSI (modern variant)
        private static double CalculateEMA(IReadOnlyList<double> closes, int period)
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
    }
}
