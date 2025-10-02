namespace MarketScanner.Data.Models
{
    public class EquityScanResult
    {
        public string Symbol { get; set; } = string.Empty;
        public double Price { get; set; }
        public double RSI { get; set; }
        public double SMA { get; set; }
        public double Upper { get; set; }
        public double Lower { get; set; }
        public double Volume { get; set; }

        // New helper property for UI
        public string VolumeDisplay => double.IsNaN(Volume) ? "N/A" : Volume.ToString("F2");
    }
}
