using MarketScanner.Core.Indicators;
using MarketScanner.Core.Models;

namespace MarketScanner.Core.Classification
{
    public class CreeperClassifier : IEquityClassifier
    {
        private readonly CreeperCriteria _criteria;
        private sealed record TrendMetrics(double PctAboveBaseline, double MaxDeviationPct, double SlopePct);

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

            CreeperEvaluation evaluation = Evaluate(trendMetrics);

            result.CreeperScore = evaluation.Score;
            result.IsCreeper = evaluation.IsCreeper;

            if(evaluation.IsCreeper)
            {
                result.CreeperType = evaluation.Type;
                result.Tags.Add("Creeper");
                result.Tags.Add($"Creeper:{evaluation.Type}");
            }
        }

        private CreeperEvaluation Evaluate(TrendMetrics metrics)
        {
            if (!PassesHardFilters(metrics))
                return CreeperEvaluation.NotCreeper();

            double trendScore = ComputeTrendScore(metrics);

            double totalScore = trendScore;

            int finalScore = (int)Math.Round(totalScore);

            if(finalScore < _criteria.ScoreThreshold)
                return new CreeperEvaluation {  Score = finalScore };
            return new CreeperEvaluation
            {
                IsCreeper = true,
                Score = finalScore,
                Type = InferType(metrics)
            };
        }

        private bool PassesHardFilters(TrendMetrics metrics)
        {
            if (metrics.PctAboveBaseline < _criteria.MinBarsAboveBaselinePct)
                return false;
            if (metrics.MaxDeviationPct > _criteria.MaxBaselineDeviationPct)
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
        private double ComputeVolatilityScore(IReadOnlyList<Bar> bars) => 50;
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
