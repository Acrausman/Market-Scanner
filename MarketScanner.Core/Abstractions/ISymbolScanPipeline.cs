using System.Threading;
using System.Threading.Tasks;
using MarketScanner.Core.Models;

namespace MarketScanner.Core.Abstractions
{
    public interface ISymbolScanPipeline
    {
        public Task<EquityScanResult> ScanAsync(TickerInfo info, CancellationToken cancellationToken);
    }
}
