namespace MarketScanner.Core.Models
{
    /// <summary>
    /// Provides detailed information about a ticker
    /// </summary>
    public class TickerInfo
    {
        /// <summary>
        /// Symbol name
        /// </summary>
        public string Symbol { get; set; } = string.Empty;
        /// <summary>
        /// Country of origin
        /// </summary>
        public string Country { get; set; } = "US";
        /// <summary>
        /// Equity sector
        /// </summary>
        public string Sector { get; set; } = "Unknown";
        /// <summary>
        /// Equity price
        /// </summary>
        public double Price { get; set; } = double.NaN;
    }
}
