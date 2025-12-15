using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace MarketScanner.UI.Wpf.ViewModels
{
    public partial class FilterPanelViewModel : ObservableObject
    {
        [ObservableProperty]
        public double? minPrice;
        [ObservableProperty]
        public double? maxPrice;

        public ObservableCollection<string> AvailableSectors { get; } = new();
        public ObservableCollection<string> SelectedSectors { get; } = new();
        public ObservableCollection<string> AvailableCountries { get; } = new();
        public ObservableCollection<string> SelectedCountries { get; } = new();
        public string SectorHeaderText =>
            SelectedSectors.Count == 0
                ? "Sectors"
                : $"Sectors ({SelectedSectors.Count} selected)";
        public string CountryHeaderText =>
            SelectedCountries.Count == 0
                ? "Countries"
                : $"Countries ({SelectedCountries.Count} selected)";
        public event Action? FiltersApplied;
        public event Action? FiltersCleared;

        [RelayCommand]
        private void ApplyFilters()
        {
            FiltersApplied?.Invoke();
        }
        [RelayCommand]
        private void ClearFilters()
        {
            FiltersCleared?.Invoke();
        }

        public FilterPanelViewModel()
        {
            SelectedSectors.CollectionChanged += (_, __) =>
            {
                OnPropertyChanged(nameof(SectorHeaderText));
            };
            SelectedCountries.CollectionChanged += (_, __) =>
            {
                OnPropertyChanged(nameof(CountryHeaderText));
            };
        }
    }
}
