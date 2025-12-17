using MarketScanner.Core.Enums;
using MarketScanner.Core.Models;
using MarketScanner.Core.Indicators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketScanner.Data.Services
{
    public class IndicatorService: IIndicatorService
    {
        public IndicatorBundle CalculateIndicators(
            List<double> closes,
            int period,
            RsiSmoothingMethod smoothingMethod)
        {
            var result = new IndicatorBundle();
            result.RSI = RsiCalculator.Calculate(closes, period, smoothingMethod);
            result.SMA = SmaCalculator.Calculate(closes, period);
            var (middle, upper, lower) = BollingerBandsCalculator.Calculate(closes, period);
            result.UpperBand = upper;
            result.LowerBand = lower;
            const int slopeLookback = 10;
            if(closes.Count > slopeLookback)
            {
                double recent = closes[^1];
                double past = closes[^slopeLookback];
                if (past > 0)
                    result.Slope = (recent - past) / past;
            }
            else
            {
                result.Slope = double.NaN;
            }
            if(!double.IsNaN(upper) && !double.IsNaN(lower) && closes[^1] > 0)
            {
                result.BollingerWidthPercent = (upper - lower) / closes[^1];
            }

            return result;
        }
    }
}
