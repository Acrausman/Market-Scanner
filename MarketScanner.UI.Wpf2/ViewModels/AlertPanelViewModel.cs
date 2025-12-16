using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarketScanner.Data.Diagnostics;
using System;
using System.Collections.ObjectModel;

namespace MarketScanner.UI.Wpf.ViewModels
{
    public partial class AlertPanelViewModel : ObservableObject
    {
        public ObservableCollection<string> OverboughtSymbols { get; } = new();
        public ObservableCollection<string> OversoldSymbols { get; } = new();
        public ObservableCollection<string> CreeperSymbols { get; } = new();

        [RelayCommand]
        private void ClearAlerts()
        {
            Console.WriteLine("Alerts cleared");
            OverboughtSymbols.Clear();
            OversoldSymbols.Clear();
            CreeperSymbols.Clear();
        }
    }
}
