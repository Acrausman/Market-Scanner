using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarketScanner.Core.Classification;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;

namespace MarketScanner.UI.Wpf.ViewModels
{
    public partial class FilterPanelViewModel : ObservableObject
    {
        private bool _supressAutoApply;

        [ObservableProperty]
        public double? minPrice;
        [ObservableProperty]
        public double? maxPrice;

        public ObservableCollection<string> AvailableSectors { get; } = new();
        public ObservableCollection<string> SelectedSectors { get; } = new();
        public ObservableCollection<string> AvailableCountries { get; } = new();
        public ObservableCollection<string> SelectedCountries { get; } = new();
        
        public ObservableCollection<CreeperTrendDirection> AvailableTrendDirections;
        [ObservableProperty]
        public CreeperTrendDirection selectedTrendDirection;
        public string SectorHeaderText =>
            SelectedSectors.Count == 0
                ? "Sectors"
                : $"Sectors ({SelectedSectors.Count} selected)";
        public string CountryHeaderText =>
            SelectedCountries.Count == 0
                ? "Countries"
                : $"Countries ({SelectedCountries.Count} selected)";
        public event Action? FiltersApplied;
        public event Action? FiltersAutoApplied;
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
                TryAutoApply();
            };
            SelectedCountries.CollectionChanged += (_, __) =>
            {
                OnPropertyChanged(nameof(CountryHeaderText));
                TryAutoApply();
            };

            PropertyChanged += OnPropertyChangedInternal;
        }

        private void OnPropertyChangedInternal(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_supressAutoApply)
                return;

            if(e.PropertyName is nameof(MinPrice)
                or nameof(MaxPrice)
                or nameof(SelectedTrendDirection))
            {
                TryAutoApply();
            }
        }

        private DispatcherTimer? _autoApplyTimer;
        private void TryAutoApply()
        {
            if (_supressAutoApply) 
                return;
            _autoApplyTimer ??= new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _autoApplyTimer.Stop();
            _autoApplyTimer.Tick -= AutoApplyTick;
            _autoApplyTimer.Tick += AutoApplyTick;
            _autoApplyTimer.Start();
        }
        private void AutoApplyTick(Object? sender, EventArgs e)
        {
            _autoApplyTimer?.Stop();
            FiltersAutoApplied?.Invoke();
        }

        public void BeginBulkUpdate() => _supressAutoApply = true;
        public void EndBulkUpdate() => _supressAutoApply = false;

    }
}
