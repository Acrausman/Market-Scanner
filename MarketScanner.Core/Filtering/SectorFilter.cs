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

        public bool Matches(EquityScanResult info)
            => string.Equals(info.MetaData.Sector, Sector, StringComparison.OrdinalIgnoreCase);

    }
}
