using MarketScanner.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketScanner.Core.Filtering
{
    public class MultiSectorFilter: IFilter
    {
        private readonly HashSet<string> _sectors;
        public MultiSectorFilter(IEnumerable<string> sectors)
        {
            _sectors = new HashSet<string>(
                sectors.Where(s => !string.IsNullOrWhiteSpace(s)),
                StringComparer.OrdinalIgnoreCase);
        }
        public string Name => "Sectors";

        public bool Matches(EquityScanResult r)
        {
            if (_sectors.Count == 0)
                return true;
            if (r == null || r.Sector == null)
                return false;
            return _sectors.Contains(r.Sector);
        }
    }
}
