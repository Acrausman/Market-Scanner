using MarketScanner.Data.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace MarketScanner.UI.Wpf.ViewModels
{
    public class ScannerViewModel : INotifyPropertyChanged
    {
        private readonly IEquityScannerService _scannerService;
        private readonly Dispatcher _dispatcher;
        private CancellationTokenSource? _scanCts;
        private int _progressValue;

        public ObservableCollection<string> OverboughtSymbols => _scannerService.OverboughtSymbols;
        public ObservableCollection<string> OversoldSymbols => _scannerService.OversoldSymbols;

        public int ProgressValue
        {
            get => _progressValue;
            private set => SetProperty(ref _progressValue, value);
        }

        public ScannerViewModel(IEquityScannerService scannerService, Dispatcher? dispatcher = null)
        {
            _scannerService = scannerService ?? throw new ArgumentNullException(nameof(scannerService));
            _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
        }

        public async Task StartScanAsync(IProgress<int>? progress, CancellationToken cancellationToken)
        {
            if (_scanCts != null)
            {
                throw new InvalidOperationException("A scan is already in progress.");
            }

            _dispatcher.InvokeAsync(() => ProgressValue = 0);
            _scanCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _scanCts.Token;

            try
            {
                var combinedProgress = new Progress<int>(value =>
                {
                    _dispatcher.InvokeAsync(() => ProgressValue = value);
                    progress?.Report(value);
                });

                await _scannerService.ScanAllAsync(combinedProgress, token).ConfigureAwait(false);
            }
            finally
            {
                _scanCts.Dispose();
                _scanCts = null;
            }
        }

        public void Stop()
        {
            _scanCts?.Cancel();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
            {
                return false;
            }

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
