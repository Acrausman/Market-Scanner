using YahooFinanceApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MarketScanner.Data.Providers
{
    public class YahooMarketDataProvider : IMarketDataProvider
    {
        public async Task<(double price, double volume)> GetQuoteAsync(string symbol)
        {
            var securities = await Yahoo.Symbols(symbol)
                .Fields(Field.RegularMarketPrice, Field.RegularMarketVolume)
                .QueryAsync();

            if (!securities.ContainsKey(symbol))
                throw new Exception($"Symbol {symbol} not found.");

            double price = (double)securities[symbol][Field.RegularMarketPrice];
            double volume = (double)securities[symbol][Field.RegularMarketVolume];
            return (price, volume);
        }

        public async Task<IReadOnlyList<double>> GetHistoricalClosesAsync(string symbol, int days)
        {
            var history = await Yahoo.GetHistoricalAsync(
                symbol,
                DateTime.UtcNow.AddDays(-days),
                DateTime.UtcNow,
                Period.Daily);

            return history.Select(h => (double)h.Close).ToList();
        }
    }
}
