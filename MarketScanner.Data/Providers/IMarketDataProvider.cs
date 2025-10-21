using System.Collections.Generic;
using System.Threading.Tasks;

namespace MarketScanner.Data.Providers
{
    public interface IMarketDataProvider
    {
        Task<(double price, double volume)> GetQuoteAsync(string symbol);
        Task<List<double>> GetHistoricalClosesAsync(string symbol, int limit= 50, bool adjusted = true);
        Task<List<DateTime>> GetHistoricalTimestampsAsync(string symbol, int limit = 50, bool adjusted = true);
        Task<List<Bar>> GetHistoricalBarsAsync(string symbol, int limit = 50, bool adjusted = true);
        Task<List<string>> GetAllTickersAsync();

    }

}
