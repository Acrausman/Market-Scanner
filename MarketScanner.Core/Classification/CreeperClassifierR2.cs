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

        #region Diagnostic
        private static int _entered;
        private static int _failRsi;
        private static int _failSlope;
        private static int _failBbWidth;
        private static int _failVol;
        private static int _passed;
        #endregion

        public CreeperClassifierR2(CreeperCriteriaR2 criteria)
        {
            _criteria = criteria;
        }
        public void Classify(EquityScanResult result)
        {
            Interlocked.Increment(ref _entered);

            var bars = result.MetaData?.Bars;
            if (bars == null || bars.Count == 0)
                return;

            if (double.IsNaN(result.RSI))
                return;
            if (result.RSI < _criteria.MinRsi ||
                result.RSI > _criteria.MaxRsi)
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
            if (double.IsNaN(slope) || slope < _criteria.MinSlopePct)
            {
                Interlocked.Increment(ref _failSlope);
                return;
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
