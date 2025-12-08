using System;
using System.Threading;

namespace MarketScanner.Core.Progress
{
    public interface IProgressService
    {
        ScanProgressTracker CreateTracker(int totalSymbols);
        void Increment(ScanProgressTracker tracker);
        void TryReport(ScanProgressTracker tracker, IProgress<int>? progress);
    }
}
