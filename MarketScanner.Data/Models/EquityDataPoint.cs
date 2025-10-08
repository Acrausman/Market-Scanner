using System;

namespace MarketScanner.Data.Models
{
    namespace MarketScanner.Data.Models
    {
        public class  EquityDataPoint
        {
            public DateTime Timestamp { get; set; }
            public double Price { get; set; }
            public double RSI { get; set; }
            public double SMA { get; set; }
            public double UpperBand { get; set; }
            public double LowerBand { get; set; }
            public double Volume { get; set; }
        }
    }

}
