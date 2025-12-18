using MarketScanner.Core.Indicators;
using MarketScanner.Core.Models;

namespace MarketScanner.Core.Classification
{
    public class CreeperClassifier : IEquityClassifier
    {
        private readonly CreeperCriteria _criteria;
        private sealed record TrendMetrics(double PctAboveBaseline, double MaxDeviationPct, double SlopePct);
        private sealed record VolatilityMetrics(double AtrPctOfPrice, double AtrCompressionRatio);
        private sealed record PullbackMetrics(double MaxDrawdownPct, int MaxConsecutiveDownBars, int WorstRecoveryBars);

        public CreeperClassifier(CreeperCriteria criteria)
        {
            _criteria = criteria;
        }

        public void Classify(EquityScanResult result)
        {
            var bars = result.MetaData?.Bars;
            if (bars == null || bars.Count < _criteria.LookBackBars)
                return;

            var window = bars.
                Skip(bars.Count - _criteria.LookBackBars)
                .ToArray();

            TrendMetrics trendMetrics = ComputeTrendMetrics(window);
            VolatilityMetrics volatilityMetrics = ComputeVolatilityMetrics(window);

            CreeperEvaluation evaluation = Evaluate(trendMetrics, volatilityMetrics);

            result.CreeperScore = evaluation.Score;
            result.IsCreeper = evaluation.IsCreeper;

            if(evaluation.IsCreeper)
            {
                result.CreeperType = evaluation.Type;
                result.Tags.Add("Creeper");
                result.Tags.Add($"Creeper:{evaluation.Type}");
            }
        }

        private CreeperEvaluation Evaluate(TrendMetrics trend, VolatilityMetrics volatility)
        {
            if (!PassesHardFilters(trend, volatility))
                return CreeperEvaluation.NotCreeper();

            double trendScore = ComputeTrendScore(trend);
            double volatilityScore = ComputeVolatilityScore(volatility);
            double totalScore = trendScore * 0.6 + volatilityScore * 0.4;


            int finalScore = (int)Math.Round(totalScore);

            if(finalScore < _criteria.ScoreThreshold)
                return new CreeperEvaluation {  Score = finalScore };
            return new CreeperEvaluation
            {
                IsCreeper = true,
                Score = finalScore,
                Type = InferType(trend)
            };
        }

        private bool PassesHardFilters(TrendMetrics trend, VolatilityMetrics volatility)
        {
            if (!PassesTrendHardFilters(trend))
                return false;
            if (_criteria.StrictMode && !PassesVolatilityFilters(volatility))
                return false;
            
            return true;
        }
        private bool PassesTrendHardFilters(TrendMetrics trend)
        {
            if(trend.PctAboveBaseline < _criteria.MinBarsAboveBaselinePct)
                return false;

            if (trend.MaxDeviationPct > _criteria.MaxBaselineDeviationPct)
                return false;

            return true;
        }
        private bool PassesVolatilityFilters(VolatilityMetrics metrics)
        {
            if (metrics.AtrPctOfPrice > _criteria.MaxAtrPctOfPrice)
                return false;

            if (metrics.AtrCompressionRatio > _criteria.AtrCompressionRatio)
                return false;

            return true;
        }
        private double ComputeTrendScore(TrendMetrics metrics)
        {
            double baselineCOmponent =
                Math.Clamp(
                    (metrics.PctAboveBaseline - _criteria.MinBarsAboveBaselinePct)
                    / (1.0 - _criteria.MinBarsAboveBaselinePct),
                    0,
                    1) * 70.0;

            double slopeComponent =
                Math.Clamp(
                    metrics.SlopePct / _criteria.MaxBaselineDeviationPct,
                    0,
                    1) * 30.0;
            return baselineCOmponent + slopeComponent;
        }
        private TrendMetrics ComputeTrendMetrics(IReadOnlyList<Bar> window)
        {
            var closes = window.Select(b => b.Close).ToArray();

            var baselineSeries =
                SmaCalculator.CalculateSeries(closes, _criteria.BaselinePeriod);

            int offset = closes.Length - baselineSeries.Count;
            int aboveCount = 0;
            double maxDeviationPct = 0;

            for(int i = 0; i < baselineSeries.Count; i++)
            {
                double price = closes[i + offset];
                double baseline = baselineSeries[i];

                if (price > baseline)
                    aboveCount++;

                double deviationPct =
                    Math.Abs(price - baseline) / baseline * 100.0;

                if (deviationPct > maxDeviationPct)
                    maxDeviationPct = deviationPct;
            }

            double pctAbove =
                (double)aboveCount / baselineSeries.Count;
            double slopePct =
                (baselineSeries.Last() - baselineSeries.First())
                / baselineSeries.First() * 100.0;

            return new TrendMetrics(
                pctAbove,
                maxDeviationPct,
                slopePct);
        }
        private VolatilityMetrics ComputeVolatilityMetrics(
            IReadOnlyList<Bar> window)
        {
            double atrShort =
                AtrCalculator.Calculate(window, _criteria.AtrPeriod);
            double atrLong =
                AtrCalculator.Calculate(window, _criteria.AtrPeriod * 3);
            double lastClose = window[^1].Close;
            double atrPct =
                atrShort / lastClose * 100;
            double compressionRatio =
                double.IsNaN(atrLong) || atrLong == 0
                ? 1.0
                : atrShort / atrLong;
            return new VolatilityMetrics(atrPct, compressionRatio);
        }
        private double ComputeVolatilityScore(VolatilityMetrics metrics)
        {
            double atrScore =
                Math.Clamp(
                    1.0 - (metrics.AtrPctOfPrice / _criteria.MaxAtrPctOfPrice),
                    0,
                    1);

            double compressionScore =
                Math.Clamp(
                    1.0 - (metrics.AtrCompressionRatio/ _criteria.AtrCompressionRatio),
                    0,
                    1);
            return (atrScore * 0.6 + compressionScore * 0.4) * 100.0;
        }
        private PullbackMetrics ComputePullbackMetrics(IReadOnlyList<Bar> window)
        {
            double peak = window[0].Close;
            double maxDrawdownPct = 0;

            int consecutiveDown = 0;
            int maxConsecutiveDown = 0;

            int recoveryBars = 0;
            int worstRecoveryBars = 0;
            bool inDrawdown = false;

            for (int i = 1; i < window.Count; i++)
            {
                double close = window[i].Close;
                double prevClose = window[i - 1].Close;

                if (close > peak)
                {
                    peak = close;

                    if (inDrawdown)
                    {
                        worstRecoveryBars = Math.Max(worstRecoveryBars, recoveryBars);
                        recoveryBars = 0;
                        inDrawdown = false;
                    }
                }
                else
                {
                    inDrawdown = true;
                    recoveryBars++;

                    double drawdownPct =
                        (peak - close) / peak * 100.0;

                    if (drawdownPct > maxDrawdownPct)
                        maxDrawdownPct = drawdownPct;
                }

                if (close < prevClose)
                {
                    consecutiveDown++;
                    if (consecutiveDown > maxConsecutiveDown)
                        maxConsecutiveDown = consecutiveDown;
                }
                else
                {
                    consecutiveDown = 0;
                }
            }

            if (inDrawdown)
                worstRecoveryBars = Math.Max(worstRecoveryBars, recoveryBars);

            return new PullbackMetrics(
                maxDrawdownPct,
                maxConsecutiveDown,
                worstRecoveryBars);
        }

        private double ComputePullbackScore(IReadOnlyList<Bar> bars) => 50;
        private double ComputeSmoothnessScore(IReadOnlyList<Bar> bars) => 50;
        private CreeperType InferType(TrendMetrics metrics)
        {
            if (metrics.SlopePct > 0.2)
                return CreeperType.Uptrend;
            if (metrics.SlopePct < -0.2)
                return CreeperType.Downtrend;

            return CreeperType.Accumulation;
        }

        private sealed class CreeperEvaluation
        {
            public bool IsCreeper { get; init; }
            public int Score { get; init; }
            public CreeperType Type { get; init; }

            public static CreeperEvaluation NotCreeper() =>
                new() { IsCreeper = false, Score = 0 };
        }


    }

}
