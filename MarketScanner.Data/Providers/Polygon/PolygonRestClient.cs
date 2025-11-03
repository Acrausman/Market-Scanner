using Flurl;
using Flurl.Http;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MarketScanner.Data.Providers.Polygon
{
    internal class PolygonRestClient
    {
        private readonly string _apiKey;

        public PolygonRestClient(string apiKey)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        }

        public Task<JObject> GetJsonAsync(string url, CancellationToken cancellationToken)
        {
            var request = new Flurl.Url(url).SetQueryParam("apiKey", _apiKey);
            return request.GetJsonAsync<JObject>(cancellationToken);
        }

        public Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
        {
            var request = new Flurl.Url(url).SetQueryParam("apiKey", _apiKey);
            return request.GetStringAsync(cancellationToken);
        }
    }
}
