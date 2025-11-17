using MarketScanner.Core.Models;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MarketScanner.Core.Metadata
{
    public class TickerMetadataCache
    {
        private readonly string _cacheFilePath;
        private readonly ConcurrentDictionary<string, TickerInfo> _cache = new();

        public TickerMetadataCache(string cacheFilePath)
        {
            _cacheFilePath = cacheFilePath;
            Load();
        }

        public bool TryGet(string symbol, out TickerInfo? info)
            => _cache.TryGetValue(symbol, out info);

        public void AddOrUpdate(TickerInfo info)
        {
            _cache[info.Symbol] = info;
            Save();
        }

        private void Load()
        {
            if (!File.Exists(_cacheFilePath))
                return;

            var json = File.ReadAllText(_cacheFilePath);
            var data = JsonSerializer.Deserialize<Dictionary<string, TickerInfo>>(json);

            if(data != null)
            {
                foreach (var kv in data)
                    _cache[kv.Key] = kv.Value;
            }
        }

        private void Save()
        {
            var json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_cacheFilePath, json);
        }
    }
}
