using MarketScanner.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketScanner.Core.Classification
{
    public class CreeperDiagnosticsClassifier : IEquityClassifier
    {
        private readonly int _smaPeriod;
        private readonly int _slopeLookback;
        private readonly int _volLookback;

        public CreeperDiagnosticsClassifier(
            int smaPeriod = 20,
            int slopeLookback = 5,
            int volLookback = 14)
        {
            _smaPeriod = smaPeriod;
            _slopeLookback = slopeLookback;
            _volLookback = volLookback;
        }

        public void Classify(EquityScanResult result)
        {
            var bars = result.MetaData?.Bars;
            if (bars == null || bars.Count == 0)
                return;
            var closes = bars.Select(b => b.Close).ToList();

            double slope =
                CreeperSignalsR2.ComputeSmaSlope(
                    closes,
                    _smaPeriod,
                    _slopeLookback);
            double bbWidth =
                CreeperSignalsR2.ComputeBollingerWidthPct(
                    result.Upper,
                    result.Lower,
                    result.Price);
            double returnStdDev =
                CreeperSignalsR2.ComputeReturnStdDev(
                    bars,
                    _volLookback);
            Console.WriteLine(
                $"[CREEPER DIAGNOSTIC] {result.Symbol} " +
                $"Slope={slope:F5}" +
                $"BBWidth={bbWidth:F4}" +
                $"RSI={result.RSI:F1}" +
                $"RetStd={returnStdDev:F4}");
        }
    }
}
