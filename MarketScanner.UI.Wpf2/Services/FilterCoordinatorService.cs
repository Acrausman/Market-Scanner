using System.Collections.Generic;
using System.Linq;
using MarketScanner.Core.Filtering;
using MarketScanner.Data.Services.Analysis;
using MarketScanner.UI.Wpf.ViewModels;
using MarketScanner.Core.Configuration;
using MarketScanner.Data.Services;

namespace MarketScanner.UI.Wpf.Services
{
    public class FilterCoordinatorService
    {
        private readonly IFilterService _filterService;
        private readonly AppSettings _settings;

        public FilterCoordinatorService(
            IFilterService filterService,
            AppSettings settings )
        {
            _filterService = filterService;
           _settings = settings;
        }

        public void ApplyFilters(FilterPanelViewModel vm)
        {
            var filters = new List<IFilter>();

            if (vm.MinPrice.HasValue || vm.MaxPrice.HasValue)
                filters.Add(new PriceFilter(vm.MinPrice, vm.MaxPrice));
            if (vm.SelectedSectors.Any())
                filters.Add(new MultiSectorFilter(vm.SelectedSectors));
            if (vm.SelectedCountries.Any())
                filters.Add(new MultiCountryFilter(vm.SelectedCountries));

            _filterService.ClearFilters();
            _filterService.AddMultipleFilters(filters);

            _settings.FilterMinPrice = vm.MinPrice;
            _settings.FilterMaxPrice = vm.MaxPrice;
            _settings.FilterSectors = vm.SelectedSectors.ToList();
            _settings.FilterCountries = vm.SelectedCountries.ToList();
            _settings.Save();
        }

        public void ClearFilters(FilterPanelViewModel vm)
        {
            vm.MinPrice = null;
            vm.MaxPrice = null;

            vm.SelectedSectors.Clear();
            vm.SelectedCountries.Clear();

            _filterService.ClearFilters();

            _settings.FilterMinPrice = null; 
            _settings.FilterMaxPrice = null;
            _settings.FilterSectors.Clear();
            _settings.FilterCountries.Clear();
            _settings.Save();
            
        }

        public void LoadFiltersFromSettings(FilterPanelViewModel vm)
        {
            vm.MinPrice = _settings.FilterMinPrice;
            vm.MaxPrice = _settings.FilterMaxPrice;
            vm.SelectedSectors.Clear();
            foreach (var s in _settings.FilterSectors)
                vm.SelectedSectors.Add(s);
            vm.SelectedCountries.Clear();
            foreach (var c in _settings.FilterCountries)
                vm.SelectedCountries.Add(c);
        }
    }
}
