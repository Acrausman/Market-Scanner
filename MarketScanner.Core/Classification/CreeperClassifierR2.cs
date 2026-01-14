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

        public CreeperClassifierR2(CreeperCriteriaR2 criteria)
        {
            _criteria = criteria;
        }
        public void Classify(EquityScanResult result)
        {

            var bars = result.MetaData?.Bars;
            if (bars == null || bars.Count == 0)
                return;

            if (double.IsNaN(result.RSI))
                return;
            if (result.RSI < _criteria.MinRsi ||
                result.RSI > _criteria.MaxRsi)
                return;
            var closes = bars.Select(b => b.Close).ToList();

            double slope =
                CreeperSignalsR2.ComputeSmaSlope(
                    closes,
                    _criteria.SmaPeriod,
                    _criteria.SlopeLookback);
            if (double.IsNaN(slope) || slope < _criteria.MinSlopePct)
                return;

            double bbWidth =
                CreeperSignalsR2.ComputeBollingerWidthPct(
                    result.Upper,
                    result.Lower,
                    result.Price);
            if(double.IsNaN(bbWidth) || bbWidth > _criteria.MaxBollingerWidthPct)
                return;

            double returnStd =
                CreeperSignalsR2.ComputeReturnStdDev(
                    bars,
                    _criteria.VolatilityLookback);
            if (double.IsNaN(returnStd) || returnStd > _criteria.MaxReturnStdDev)
                return;

            //Has passed filters
            Logger.WriteLine($"[R2] {result.Symbol} result hash = {result.GetHashCode()}");
            result.IsCreeper = true;
            result.Tags.Add("Creeper");
            result.Tags.Add("CreeperR2");
        }
        
    }
}
