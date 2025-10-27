using MarketScanner.Data.Services;
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
        private CancellationTokenSource _cts;

        public ObservableCollection<string> OverboughtSymbols => _scanner.OverboughtSymbols;
        public ObservableCollection<string> OversoldSymbols => _scanner.OversoldSymbols;

        public IProgress<int> ScanProgress { get; }
        public ScannerViewModel(EquityScannerService scanner)
        {
            _scanner = scanner;

            ScanProgress = new Progress<int>(val =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProgressValue = val;
                });
            });
            ProgressValue = 0;
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
