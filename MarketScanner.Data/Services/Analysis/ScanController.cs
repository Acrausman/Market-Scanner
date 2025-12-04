using MarketScanner.Core.Abstractions;
using MarketScanner.Data.Diagnostics;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MarketScanner.Data.Services.Analysis
{
    public class ScanController : IScanController
    {
        private readonly ManualResetEventSlim _pauseEvent = new(true);
        private readonly SemaphoreSlim _restartLock = new(1, 1);

        private CancellationTokenSource? _scanCts;
        private Task? _scanTask;
        private bool _isScanning;

        public bool IsScanning => _isScanning;

        public async Task StartAsync(
            Func<CancellationToken, Task> scanOperation,
            IProgress<int>? progress)
        {
            if (_isScanning)
                return;

            _pauseEvent.Set();
            _isScanning = true;

            _scanCts = new CancellationTokenSource();
            var token = _scanCts.Token;

            _scanTask = Task.Run(async () =>
            {
                try
                {
                    await scanOperation(token).ConfigureAwait(false);
                }
                catch(OperationCanceledException)
                {
                    //ignore
                }
                finally
                {
                    _isScanning = false;
                }
            });
        }

        public async Task RestartAsync(
            Func<CancellationToken, Task> scanOperation,
            IProgress<int>? progress)
        {
            await _restartLock.WaitAsync();
            try
            {
                if (_isScanning)
                    await StopAsync().ConfigureAwait(false);
                await StartAsync(scanOperation, progress).ConfigureAwait(false);
            }
            finally
            {
                _restartLock.Release();
            }
        }

        public async Task StopAsync()
        {
            if (!_isScanning || _scanCts == null)
                return;

            try
            {
                _scanCts.Cancel();
            }
            catch { }

            if(_scanTask != null)
            {
                try
                {
                    await _scanTask.ConfigureAwait(false);
                }
                catch(OperationCanceledException) { }
            }

            _scanCts.Dispose();
            _scanCts = null;
        }

        public void Pause() => _pauseEvent.Reset();
        public void Resume() => _pauseEvent.Set();

        public void WaitForResume(CancellationToken token)
        {
            _pauseEvent.Wait(token);
        }
    }
}
