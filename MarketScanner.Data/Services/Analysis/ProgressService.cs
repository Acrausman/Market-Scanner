using MarketScanner.Core.Progress;
using System;

namespace MarketScanner.Data.Services.Analysis
{
    public class ProgressService : IProgressService
    {
        public ScanProgressTracker CreateTracker(int totalSymbols)
        {
            return new ScanProgressTracker(totalSymbols);
        }

        public void Increment(ScanProgressTracker tracker)
        {
            tracker.Increment();
        }

        public void TryReport(ScanProgressTracker tracker, IProgress<int>? progress)
        {
            if (progress == null)
                return;

            if (tracker.ShouldReport())
                progress.Report(tracker.GetPercentage());tracker.ResetBatch();
        }
    }
}
