using System;
using System.Collections.ObjectModel;
using System.Windows.Documents;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarketScanner.Core.Enums;
using MarketScanner.Data.Services;

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
        private readonly EmailService _emailService;
        public SettingsPanelViewModel(EmailService emailService)
        {
            _emailService = emailService;
        }

        [RelayCommand]
        private void ApplySettings() =>
            SettingsApplied?.Invoke();
        [RelayCommand]
        private void ResetSettings() => 
            SettingsReset?.Invoke();
        [RelayCommand]
        private void SaveEmail()
        {
            if(!string.IsNullOrWhiteSpace(EmailAddress))
                SettingsApplied?.Invoke();
        }
        [RelayCommand]
        private void TestEmail()
        {
            if(!string.IsNullOrWhiteSpace(EmailAddress))
            {
                _emailService.SendEmail(EmailAddress, "Market Scanner Test", "This is a test email from the market scanner.");
            }
        }
    }
}
