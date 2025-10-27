using MarketScanner.Data.Providers;
using MarketScanner.Data.Services;
using MarketScanner.UI.Wpf.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MarketScanner.UI.Wpf.ViewModels
{
    public class ScannerViewModel : INotifyPropertyChanged
    {
        private readonly EquityScannerService _scanner;
        private readonly IMarketDataProvider _provider;
        private readonly ChartViewModel _chartViewModel;
        private readonly ChartManager _chartManager;


        private CancellationTokenSource _cts;

        public ObservableCollection<string> OverboughtSymbols => _scanner.OverboughtSymbols;
        public ObservableCollection<string> OversoldSymbols => _scanner.OversoldSymbols;

        public IProgress<int> ScanProgress { get; }
        public ScannerViewModel(EquityScannerService scanner, ChartManager chartManager)
        {
            _scanner = scanner;
            _chartManager = chartManager;
            _chartViewModel = new ChartViewModel(_provider, _chartManager);

            ScanProgress = new Progress<int>(val =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProgressValue = val;
                });
            });
            ProgressValue = 0;
        }

        private string _selectedSymbol;
        public string SelectedSymbol
        {
            get => _selectedSymbol;
            set
            {
                if(_selectedSymbol != value)
                {
                    _selectedSymbol = value;
                    OnPropertyChanged(nameof(SelectedSymbol));

                    if(!string.IsNullOrEmpty(_selectedSymbol))
                        _chartViewModel.LoadChartForSymbol(_selectedSymbol);
                        
                }
            }
        }
        private int _progressValue;
        public int ProgressValue
        {
            get => _progressValue;
            set
            {
                if (_progressValue != value)
                {
                    _progressValue = value;
                    OnPropertyChanged(nameof(ProgressValue));
                }
            }
        }


        public async Task StartAsync(IProgress<int> progress)
        {
            _cts = new CancellationTokenSource();
            Console.WriteLine("[UI] Starting full equity scan...");

            try
            {
                await _scanner.ScanAllAsync(ScanProgress, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[UI] Scan canceled.");
            }
            finally
            {
                _cts = null;
            }
        }


        public void Stop() => _cts?.Cancel();

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
