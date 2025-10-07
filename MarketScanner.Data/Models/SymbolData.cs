using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OxyPlot;

namespace MarketScanner.Data.Models
{
    public class SymbolData
    {
        public List<DataPoint> PricePoints { get; set; } = new();
        public List<DataPoint> SmaPoints { get; set; } = new();
        public List<(DataPoint upper, DataPoint lower)> BollingerBands { get; set; } = new();
        public List<DataPoint> RsiPoints { get; set; } = new();
        public List<DataPoint> VolumePoints { get; set; } = new();
    }
}

