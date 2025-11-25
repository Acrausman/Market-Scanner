using MarketScanner.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketScanner.Core.Filtering
{
    public class MultiCountryFilter: IFilter
    {
        private readonly HashSet<String> _countries;
        public string Name => "Countries";
        public MultiCountryFilter(IEnumerable<string> countries)
        {
            _countries = new HashSet<string>(
                countries.Where(c => !string.IsNullOrWhiteSpace(c)),
                StringComparer.OrdinalIgnoreCase);
        }

        public bool Matches(TickerInfo r)
        {
            if (_countries.Count == 0)
                return true;
            if (r == null || r.Country == null)
                return false;
            return _countries.Contains(r.Country);
        }
    }
}
