using MarketScanner.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketScanner.Core.Filtering
{
    /// <summary>
    /// Filters by country of origin
    /// </summary>
    public class CountryFilter: IFilter
    {
        /// <summary>
        /// Name of country
        /// </summary>
        public string Name => "Country";
        /// <summary>
        /// Country code
        /// </summary>
        public string CountryCode { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="countryCode"></param>
        public CountryFilter(string countryCode)
        {
            CountryCode = countryCode;
        }

        /// <summary>
        /// Whether or not the filter applies
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>

        public bool Matches(EquityScanResult result)
            => string.Equals(result.Country, CountryCode, StringComparison.OrdinalIgnoreCase);
        /// <summary>
        /// Whether or not the filter applies(ticker)
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        public bool MatchesTicker(TickerInfo info)
            => string.Equals(info.Country, CountryCode, StringComparison.OrdinalIgnoreCase);
    }
}
