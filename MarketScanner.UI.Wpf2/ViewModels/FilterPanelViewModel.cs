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

        public ObservableCollection<string> SelectedSectors { get; } = new();
        public ObservableCollection<string> SelectedCountries { get; } = new();
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
    }
}
