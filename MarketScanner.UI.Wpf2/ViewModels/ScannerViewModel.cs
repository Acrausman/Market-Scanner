using MarketScanner.Data.Services;
using System;
using System.Collections.Generic;
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
        private CancellationTokenSource _scanCts;
        private int _progressValue;

        public int ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        public ScannerViewModel(IEquityScannerService scannerService, Dispatcher dispatcher = null)
        {
            _scannerService = scannerService ?? throw new ArgumentNullException(nameof(scannerService));
            _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
        }

        public async Task StartScanAsync(IProgress<int>? progress, CancellationToken externalToken)
        {
            // prevent overlapping scans
            if (_scanCts != null)
                throw new InvalidOperationException("A scan is already in progress.");

            // reset progress on UI thread
            await _dispatcher.InvokeAsync(() => ProgressValue = 0);

            // create a linked CTS (so UI cancel or parent cancel both work)
            _scanCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
            var token = _scanCts.Token;

            try
            {
                // wrap for combined UI + external progress reporting
                var combinedProgress = new Progress<int>(value =>
                {
                    _dispatcher.InvokeAsync(() => ProgressValue = value);
                    progress?.Report(value);
                });

                // run scan with new cancellation + batching logic
                await _scannerService.ScanAllAsync(combinedProgress, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await _dispatcher.InvokeAsync(() =>
                {
                    ProgressValue = 0;
                    // optional: log or update a StatusText property
                    System.Diagnostics.Debug.WriteLine("[UI] Scan cancelled by user.");
                });
            }
            catch (Exception ex)
            {
                await _dispatcher.InvokeAsync(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"[UI] Scan failed: {ex.Message}");
                });
            }
            finally
            {
                _scanCts?.Dispose();
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
