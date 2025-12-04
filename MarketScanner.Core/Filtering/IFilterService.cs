using System.Collections.Generic;
using MarketScanner.Core.Filtering;
using MarketScanner.Core.Models;

namespace MarketScanner.Core.Filtering
{
    public interface IFilterService
    {
        void AddFilter(IFilter filter);
        void AddMultipleFilters(IEnumerable<IFilter> filters);
        void ClearFilters();
        bool PassesFilters(EquityScanResult result);
        IReadOnlyList<IFilter> Filters { get; }
    }
}
