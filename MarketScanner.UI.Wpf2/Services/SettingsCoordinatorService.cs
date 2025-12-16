using MarketScanner.UI.Wpf.ViewModels;
using MarketScanner.Core.Configuration;
using System.Linq;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace MarketScanner.UI.Wpf.Services
{
    public class SettingsCoordinatorService
    {
        private readonly AppSettings _settings;

        public SettingsCoordinatorService(AppSettings settings)
        {
            _settings = settings;
        }

        public void LoadInto(SettingsPanelViewModel vm)
        {
            vm.EmailAddress = _settings.NotificationEmail;
            vm.IndicatorPeriod = _settings.IndicatorPeriod;
            vm.SmoothingMethod = _settings.RsiMethod;
            vm.SelectedTimespan = _settings.SelectedTimespan;
            vm.AlertIntervalMinutes = _settings.AlertIntervalMinutes;
        }

        public void Apply(SettingsPanelViewModel vm)
        {
            _settings.NotificationEmail = vm.EmailAddress ?? string.Empty;
            _settings.IndicatorPeriod = vm.IndicatorPeriod;
            _settings.RsiMethod = vm.SmoothingMethod;
            _settings.SelectedTimespan = vm.SelectedTimespan;
            _settings.AlertIntervalMinutes = vm.AlertIntervalMinutes;

            _settings.Save();
        }

        public void Reset(SettingsPanelViewModel vm)
        {
            vm.EmailAddress = string.Empty;
            vm.IndicatorPeriod = 14;
            vm.SmoothingMethod = Core.Enums.RsiSmoothingMethod.Simple;
            vm.SelectedTimespan = "3M";
            vm.AlertIntervalMinutes = 15;

            Apply(vm);
        }
    }
}
