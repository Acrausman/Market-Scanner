namespace MarketScanner.Data.Models
{
    public class Alert
    {
        public string? Symbol { get; set; }
        public double? RsiAbove { get; set; }
        public double? RsiBelow { get; set; }
        public double? PriceAbove { get; set; }
        public double? PriceBelow { get; set; }
        public string? Message { get; set; }
        public bool isTriggered { get; set; } = false;
    }
}
