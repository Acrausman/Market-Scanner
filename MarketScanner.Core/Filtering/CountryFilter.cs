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

        public CountryFilter(string countryCode)
        {
            CountryCode = countryCode;
        }

        public bool Matches(EquityScanResult info)
            => string.Equals(info.MetaData.Country, CountryCode, StringComparison.OrdinalIgnoreCase);
    }
}
