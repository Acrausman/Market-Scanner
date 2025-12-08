using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketScanner.Core.Progress
{
    public class ScanProgressTracker
    {
        private readonly int _totalSymbols;
        private int _processedSymbols;
        private int _batchCount;

        private readonly int _batchSize = 10;
        public ScanProgressTracker(int totalSymbols)
        {
            _totalSymbols = Math.Max(totalSymbols, 1);
            _processedSymbols = 0;
            _batchSize = 0;
        }
        public void Increment()
        {
            Interlocked.Increment(ref _processedSymbols);
        }
        public int GetPercentage()
        {
            var value = (int)((double)_processedSymbols / _totalSymbols * 100.0);
            return Math.Clamp(value, 0, 100);
        }
        public bool ShouldReport()
        {
            return Interlocked.Increment(ref _batchCount) >= _batchSize;
        }
        public void ResetBatch()
        {
            _batchCount = 0;
        }
    }
}
