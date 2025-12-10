using System;
using System.Collections.ObjectModel;
using System.Windows.Documents;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarketScanner.Core.Enums;

namespace MarketScanner.UI.Wpf.ViewModels
{
    public partial class SettingsPanelViewModel : ObservableObject
    {
        [ObservableProperty]
        private string? emailAddress;
        [ObservableProperty]
        private int indicatorPeriod;
        [ObservableProperty]
        private RsiSmoothingMethod smoothingMethod;
        [ObservableProperty]
        private string selectedTimespan;
        [ObservableProperty]
        private int alertIntervalMinutes;

        public event Action? SettingsApplied;
        public event Action? SettingsReset;

        [RelayCommand]
        private void ApplySettings() =>
            SettingsApplied?.Invoke();
        [RelayCommand]
        private void ResetSettings() => 
            SettingsReset?.Invoke();
    }
}
