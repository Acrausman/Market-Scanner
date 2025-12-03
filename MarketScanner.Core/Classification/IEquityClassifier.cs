using MarketScanner.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketScanner.Core.Classification
{
    public interface IEquityClassifier
    {
        void Classify(EquityScanResult result);
    }
}
