namespace MarketScanner.Core.Models
{
    /// <summary>
    /// Provides detailed information about a ticker
    /// </summary>
    public class TickerInfo
    {

        public string Symbol { get; set; } = string.Empty;

        public string Country { get; set; } = "US";

        public string Sector { get; set; } = "Unknown";
        public string Exchange { get; set; } = "Unknown";

        public double Price { get; set; } = double.NaN;
        public IReadOnlyList<Bar> Bars { get; init; } = Array.Empty<Bar>();
    }
}
