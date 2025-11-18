using MarketScanner.Core.Abstractions;
using MarketScanner.Core.Metadata;
using MarketScanner.Core.Models;
using MarketScanner.Data.Models;
using MarketScanner.Data.Diagnostics;
using Newtonsoft.Json.Linq;
using Polygon.Models;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MarketScanner.Data.Providers.Finnhub
{
    public class FinnhubFundamentalProvider: IFundamentalProvider
    {
        private readonly string _apiKey;
        private readonly HttpClient _http;

        public FinnhubFundamentalProvider(string apiKey)
        {
            _apiKey = apiKey;
            _http = new HttpClient();
        }

        public async Task<TickerInfo?> GetMetadataAsync(string symbol, CancellationToken cancellationToken = default)
        {
            try
            {
                //Logger.WriteLine("[FUNDP] FinnhubFundamentalProvider.GetMetadataAsync CALLED");

                string url = $"https://finnhub.io/api/v1/stock/profile2?symbol={symbol}&token={_apiKey}";
                string json = await _http.GetStringAsync(url, cancellationToken);

                var j = JObject.Parse(json);

                // FIX: correct field name
                if (j["ticker"] == null)
                    return null;
                return new TickerInfo
                {
                    Symbol = symbol,
                    Country = j.Value<string>("country") ?? "US",
                    Sector = j.Value<string>("finnhubIndustry") ?? "Unknown",
                    Exchange = j.Value<string>("exchange") ?? ""
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
