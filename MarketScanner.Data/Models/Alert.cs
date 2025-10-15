namespace MarketScanner.Data.Models
{
    public enum AlertType
    {
        RsiOverbought,
        RsiOversold,
        PriceAbove,
        PriceBelow
    }

    public class Alert
    {
        public string? Symbol { get; set; }
        public AlertType Type { get; set; }
        public bool NotifyEmail { get; set; }
        public double? RsiAbove { get; set; }
        public double? RsiBelow { get; set; }
        public double? PriceAbove { get; set; }
        public double? PriceBelow { get; set; }
        public string? Message { get; set; }
        public double? Threshold {  get; set; }
        public DateTime? LastTriggered { get; set; }
        public bool isTriggered { get; set; } = false;
    }
}
