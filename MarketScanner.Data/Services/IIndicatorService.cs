using MarketScanner.Core.Enums;
using MarketScanner.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketScanner.Data.Services
{
    public interface IIndicatorService
    {
        IndicatorBundle CalculateIndicators(
            List<double> closes,
            int period,
            RsiSmoothingMethod smoothingMethod);
    }
}
