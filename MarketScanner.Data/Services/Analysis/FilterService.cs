using System.Collections.Generic;
using System.Linq;
using MarketScanner.Core.Filtering;
using MarketScanner.Core.Models;

namespace MarketScanner.Data.Services.Analysis
{
    public class FilterService: IFilterService
    {
        private readonly List<IFilter> _filters = new List<IFilter>();
        public IReadOnlyList<IFilter> Filters => _filters;
        public void AddFilter(IFilter filter)
        {
            _filters.Clear();
            _filters.Add(filter);
        }

        public void AddMultipleFilters(IEnumerable<IFilter> filters)
        {
            _filters.Clear();
            _filters.AddRange(filters);
        }
        public void ClearFilters()
        {
            _filters.Clear();
        }
        public bool PassesFilters(EquityScanResult result)
        {
            if (_filters.Count == 0)
                return true;
            var meta = result.MetaData;
            if (meta == null)
                return false;
            return _filters.All(f => f.Matches(meta));
        }
    }
}
