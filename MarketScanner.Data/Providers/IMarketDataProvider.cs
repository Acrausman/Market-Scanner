using MarketScanner.Data.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MarketScanner.Data.Providers
{
    public interface IMarketDataProvider
    {
        Task<(double price, double volume)> GetQuoteAsync(string symbol, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Bar>> GetHistoricalBarsAsync(string symbol, DateTime start, DateTime end, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<SplitAdjustment>> GetSplitAdjustmentsAsync(string symbol, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<string>> GetAllTickersAsync(CancellationToken cancellationToken = default);
    }

}
