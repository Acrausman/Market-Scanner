using System.Collections.Generic;
using System.Threading.Tasks;

namespace MarketScanner.Data.Providers
{
    public interface IMarketDataProvider
    {
        Task<(double price, double volume)> GetQuoteAsync(string symbol);
        Task<IReadOnlyList<double>> GetHistoricalClosesAsync(string symbol, int days);

    }

}
