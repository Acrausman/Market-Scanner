using MarketScanner.Core.Configuration;
using MarketScanner.Core.Indicators;
using MarketScanner.Core.Models;
using MarketScanner.Data.Diagnostics;
using MathNet.Numerics.Statistics;
using System.Diagnostics.SymbolStore;

namespace MarketScanner.Core.Classification
{
    public class CreeperClassifierR2 : IEquityClassifier
    {
        private readonly CreeperCriteriaR2 _criteria;
        private readonly AppSettings _settings;

        #region Diagnostic
        private static int _entered;
        private static int _failRsi;
        private static int _failSlope;
        private static int _failBbWidth;
        private static int _failVol;
        private static int _passed;
        #endregion

        public CreeperClassifierR2(CreeperCriteriaR2 criteria, AppSettings settings)
        {
            _criteria = criteria;
            _settings = settings;
        }
        public void Classify(EquityScanResult result)
        {
            Interlocked.Increment(ref _entered);

            var bars = result.MetaData?.Bars;
            if (bars == null || bars.Count == 0)
                return;

            if (double.IsNaN(result.RSI))
                return;
            (double minRsi, double maxRsi) = _settings.CreeperDirection switch
            {
                CreeperTrendDirection.Up => (50, 65),
                CreeperTrendDirection.Down => (35, 50),
                CreeperTrendDirection.Both => (35, 65),
                _ => throw new ArgumentOutOfRangeException()
            };
            if (result.RSI < minRsi ||
                result.RSI > maxRsi)
            {
                Interlocked.Increment(ref _failRsi);
                return;
            }
            var closes = bars.Select(b => b.Close).ToList();

            double slope =
                CreeperSignalsR2.ComputeSmaSlope(
                    closes,
                    _criteria.SmaPeriod,
                    _criteria.SlopeLookback);
            if (_settings.CreeperDirection == CreeperTrendDirection.Down)
            {
                Logger.WriteLine(
                    $"[DOWN TEST] {result.Symbol} slope={slope:F5} rsi={result.RSI:F1}");
            }

            switch (_settings.CreeperDirection)
            {
                case CreeperTrendDirection.Up:
                    if (slope < _criteria.MinSlopePct)
                        return;
                    break;
                case CreeperTrendDirection.Down:
                    if (slope > -_criteria.MinSlopePct)
                        return;
                    break;
                case CreeperTrendDirection.Both:
                    if (Math.Abs(slope) < _criteria.MinSlopePct)
                    {
                        Interlocked.Increment(ref _failSlope);
                        return;
                    }
                    break;
            }


            double bbWidth =
                CreeperSignalsR2.ComputeBollingerWidthPct(
                    result.Upper,
                    result.Lower,
                    result.Price);
            if(double.IsNaN(bbWidth) || bbWidth > _criteria.MaxBollingerWidthPct)
            {
                Interlocked.Increment(ref _failBbWidth);
                return;
            }

            double returnStd =
                CreeperSignalsR2.ComputeReturnStdDev(
                    bars,
                    _criteria.VolatilityLookback);
            if (double.IsNaN(returnStd) || returnStd > _criteria.MaxReturnStdDev)
            {
                Interlocked.Increment(ref _failVol);
                return;
            }
            //Has passed filters
            Interlocked.Increment(ref _passed);
            result.IsCreeper = true;
            result.Tags.Add("Creeper");
            result.Tags.Add("CreeperR2");
        }


        public void LogStats()
        {
            Logger.WriteLine(
                $"[CreeperR2Stats] Entered={_entered}, " +
                $"FailRSI={_failRsi}, " +
                $"FailSlope={_failSlope}, " +
                $"FailBB={_failBbWidth}, " +
                $"FailVol={_failVol}, " +
                $"Passed={_passed}");

        }


    }
}
