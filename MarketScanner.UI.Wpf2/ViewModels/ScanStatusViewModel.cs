using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MarketScanner.UI.Wpf.ViewModels
{
    public partial class ScanStatusViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _progressValue;
        [ObservableProperty]
        private string _progressText = "0%";
        [ObservableProperty]
        private string _statusText = "Idle";
        [ObservableProperty]
        private bool _isScanning;
        [ObservableProperty]
        private bool _isPaused;
        [ObservableProperty]
        private string _elapsedTimeText = "00:00";

        private readonly Stopwatch _stopwatch = new();
        private CancellationTokenSource? _timerCts;

        public void OnScanStarted()
        {
            IsScanning = true;
            IsPaused = false;
            StatusText = "Scanning...";

            ProgressValue = 0;
            ProgressText = "";

            StartTimer();
        }
        public void OnScanStopped()
        {
            IsScanning = false;
            IsPaused = false;
            StatusText = "Idle";

            StopTimer();
            ElapsedTimeText = "00:00";
        }
        public void OnScanPaused()
        {
            IsPaused = true;
            StatusText = "Paused";
            StopTimer();
        }
        public void OnScanResumed()
        {
            IsPaused = false;
            StatusText = "Scanning...";
            StartTimer();
        }
        public void UpdateProgress(int p)
        {
            ProgressValue = p;
            ProgressText = $" {p}%";
        }
        private void StartTimer()
        {
            _stopwatch.Restart();

            _timerCts?.Cancel();
            _timerCts = new CancellationTokenSource();

            var token = _timerCts.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(1000, token);
                    ElapsedTimeText = _stopwatch.Elapsed.ToString(@"mm\:ss");
                }
            }, token);
        }
        private void StopTimer()
        {
            _timerCts?.Cancel();
            _stopwatch.Stop();
        }
    }
}
