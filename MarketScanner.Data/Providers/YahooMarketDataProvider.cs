using YahooFinanceApi;
using Flurl.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MarketScanner.Data.Providers
{
    public class YahooMarketDataProvider : IMarketDataProvider
    {
        private const int MaxRetries = 3;
        private const int DelayMilliseconds = 1500; // 1.5 seconds between retries

        /// <summary>
        /// Get the latest price and volume for a symbol, with retries
        /// </summary>
        public async Task<(double price, double volume)> GetQuoteAsync(string symbol)
        {
            try
            {
                var securities = await Yahoo.Symbols(symbol)
                    .Fields(Field.RegularMarketPrice, Field.RegularMarketVolume)
                    .QueryAsync();

                if (!securities.ContainsKey(symbol))
                    return (0, 0);

                double price = Convert.ToDouble(securities[symbol][Field.RegularMarketPrice]);
                double volume = Convert.ToDouble(securities[symbol][Field.RegularMarketVolume]);

                return (price, volume);
            }
            catch (Flurl.Http.FlurlHttpException fex)
            {
                // Full HTTP debug info
                int statusCode = fex.Call.Response?.StatusCode ?? 0;
                string body = fex.Call.Response != null ? await fex.GetResponseStringAsync() : "<no body>";

                Console.WriteLine($"[DEBUG] FlurlHttpException for '{symbol}':");
                Console.WriteLine($"Status code: {statusCode}");
                Console.WriteLine($"Response body: {body}");

                return (0, 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Unexpected exception for '{symbol}': {ex.Message}");
                return (0, 0);
            }
        }


        /// <summary>
        /// Get historical daily closing prices for the last 'days' days, with retries
        /// </summary>
        public async Task<IReadOnlyList<double>> GetHistoricalClosesAsync(string symbol, int days)
        {
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    var history = await Yahoo.GetHistoricalAsync(
                        symbol,
                        DateTime.UtcNow.AddDays(-days),
                        DateTime.UtcNow,
                        Period.Daily);

                    return history.Select(h => (double)h.Close).ToList();
                }
                catch (FlurlHttpException fex)
                {
                    string body = fex.Call.Response != null
                        ? await fex.GetResponseStringAsync()
                        : string.Empty;

                    Console.WriteLine($"[Yahoo HTTP Error] Attempt {attempt}/{MaxRetries} for historical '{symbol}': {fex.Message}");
                    if (!string.IsNullOrEmpty(body))
                        Console.WriteLine($"Response body: {body}");

                    if (attempt == MaxRetries)
                        return new List<double>();

                    await Task.Delay(DelayMilliseconds);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Error] Unexpected error fetching historical data for '{symbol}': {ex.Message}");
                    return new List<double>();
                }
            }

            return new List<double>();
        }
    }
}
