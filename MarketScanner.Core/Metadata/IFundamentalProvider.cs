using MarketScanner.Core.Models;

namespace MarketScanner.Core.Metadata
{
    public interface IFundamentalProvider
    {
        Task<TickerInfo?> GetMetadataAsync(string symbol, CancellationToken cancellationToken = default);
    }
}
