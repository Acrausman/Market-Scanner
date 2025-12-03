using MarketScanner.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketScanner.Core.Classification
{
    public class RSIClassifier: IEquityClassifier
    {
        public void Classify(EquityScanResult result)
        {
            if (double.IsNaN(result.RSI))
                return;
            if(result.RSI >= 70)
            {
                result.IsOverbought = true;
                result.Tags.Add("Overbought");
            }

            if(result.RSI <= 30)
            {
                result.IsOversold = true;
                result.Tags.Add("Oversold");
            }
        }
    }
}
