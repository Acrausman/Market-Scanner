using MarketScanner.Data.Services;
using MarketScanner.UI.Wpf.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace MarketScanner.UI.Wpf.Services
{
    public class ScanCoordinatorService
    {
        private readonly ScannerViewModel _scannerViewModel;
        private readonly AlertPanelViewModel _alertPanelViewModel;
        private readonly EquityScannerService _scannerService;
        private readonly Dispatcher _dispatcher;

        public bool IsScanning { get; private set; }
        public bool IsPaused { get; private set; }

        public event Action? ScanStarted;
        public event Action? ScanStopped;
        public event Action<int>? ProgressChanged;

        public ScanCoordinatorService(
            ScannerViewModel scannerViewModel,
            EquityScannerService scannerService,
            Dispatcher dispatcher )
        {
            _scannerViewModel = scannerViewModel;
            _scannerService = scannerService;
            _dispatcher = dispatcher;
        }
        public async Task StartScanAsync()
        {
            if (IsScanning)
                return;
            IsScanning = true;
            ScanStarted?.Invoke();

            var progress = new Progress<int>(p =>
            {
                ProgressChanged?.Invoke(p);
            });

            try
            {
                await _scannerViewModel.StartScanAsync(progress, CancellationToken.None);
            }
            finally
            {
                IsScanning = false;
                ScanStopped?.Invoke();
            }
        }

        public void StopScan()
        {
            if (!IsScanning)
                return;

            _scannerViewModel.Stop();
        }

        public async Task RestartScanAsync()
        {
            StopScan();
            await Task.Delay(200);
            await StartScanAsync();
        }

        public void Pause()
        {
            if (!IsScanning || IsPaused)
                return;

            _scannerService.Pause();
            IsPaused = true;
        }

        public void Resume()
        {
            if (!IsScanning || IsPaused)
                return;

            _scannerService.Resume();
            IsPaused = false;
        }
    }
}
