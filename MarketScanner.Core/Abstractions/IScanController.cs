using System;
using System.Threading;
using System.Threading.Tasks;
namespace MarketScanner.Core.Abstractions
{
    public interface IScanController
    {
        public interface IScanController
        {
            bool IsScanning { get; }

            Task StartAsync(Func<CancellationToken, Task> scanOperation, IProgress<int>? progress);
            Task RestartAsync(Func<CancellationToken, Task> scanOperation, IProgress<int>? progress);
            Task StopAsync();

            void Pause();
            void Resume();
        }
    }
}
