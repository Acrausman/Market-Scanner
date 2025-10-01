using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketScanner.Data.Models
{
    public class EquityScanResult
    {
        public string Symbol {  get; set; } = string.Empty;
        public double Price { get; set; }
        public double RSI { get; set; }
        public double SMA { get; set; }
        public double Upper {  get; set; }
        public double Lower { get; set; }
        public double Volume { get; set; }
        public bool IsOverbought => RSI >= 70;
        public bool IsOversold => RSI <= 30;
    }
}
