using MarketScanner.Core.Models;

namespace MarketScanner.Core.Filtering
{
    /// <summary>
    /// Filters by sector
    /// </summary>
    public class SectorFilter: IFilter
    {
        /// <summary>
        /// Filter name
        /// </summary>
        public string Name => "Sector";

        /// <summary>
        /// Equity Sector
        /// </summary>
        public string Sector { get; set; }
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="sector"></param>
        public SectorFilter(string sector)
        {
            Sector = sector;
        }

        /// <summary>
        /// Whether or not the filter applies
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public bool Matches(EquityScanResult result)
            => string.Equals(result.Sector, Sector, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Whether or not the filter applies (ticker)
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        public bool MatchesTicker(TickerInfo info)
            => string.Equals(info.Sector, Sector, StringComparison.OrdinalIgnoreCase);

    }
}
