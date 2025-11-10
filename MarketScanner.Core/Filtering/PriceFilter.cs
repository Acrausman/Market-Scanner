using MarketScanner.Core.Models;
namespace MarketScanner.Core.Filtering
{
    /// <summary>
    /// Filters by price
    /// </summary>
    public class PriceFilter: IFilter
    {
        /// <summary>
        /// The filter name
        /// </summary>
        public string Name => "Price Range";
        /// <summary>
        /// Minimum price
        /// </summary>
        public double MinPrice { get; set; }
        /// <summary>
        /// Maximum price
        /// </summary>
        public double MaxPrice { get; set; }
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        public PriceFilter(double min, double max)
        {
            MinPrice = min;
            MaxPrice = max;
        }
        /// <summary>
        /// Whether or not the filter applies
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public bool Matches(EquityScanResult result)
            => result.Price >= MinPrice && result.Price <= MaxPrice;
        /// <summary>
        /// Whether or not the filter applies
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        public bool MatchesTicker(TickerInfo info)
            => info.Price >= MinPrice && info.Price <= MaxPrice;

    }
}
