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
        private readonly object _fileLock = new();

        public TickerMetadataCache(string fileName = "ticker_metadata.json")
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(appData, "MarketScanner");

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            _cacheFilePath = Path.Combine(dir, fileName);
            Load();
        }

        public bool TryGet(string symbol, out TickerInfo? info)
        {
            var found = _cache.TryGetValue(symbol, out info);

            if (found)
                Console.WriteLine($"[META READ] {symbol} → country={info?.Country}, sector={info?.Sector}");
            else
                Console.WriteLine($"[META READ] {symbol} not found in cache");

            return found;
        }

        public void AddOrUpdate(TickerInfo info)
        {
            _cache[info.Symbol] = info;
            Console.WriteLine($"[META WRITE] {info.Symbol} → country={info.Country}, sector={info.Sector}");

            // Save removed — now done once per scan
        }

        public void SaveCacheToDisk()
        {
            Save();
        }

        private void Load()
        {
            Console.WriteLine("[META DEBUG] Loading metadata cache...");
            Console.WriteLine("[META DEBUG] Path = " + Path.GetFullPath(_cacheFilePath));

            if (!File.Exists(_cacheFilePath))
            {
                Console.WriteLine("[META DEBUG] Cache file not found.");
                return;
            }

            try
            {
                var json = File.ReadAllText(_cacheFilePath);
                var data = JsonSerializer.Deserialize<Dictionary<string, TickerInfo>>(json);

                if (data == null)
                {
                    Console.WriteLine("[META DEBUG] Cache file exists but is empty.");
                    return;
                }

                foreach (var kv in data)
                    _cache[kv.Key] = kv.Value;

                Console.WriteLine($"[META DEBUG] Loaded {_cache.Count} total entries.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[META DEBUG] ERROR loading cache: " + ex.Message);
            }
        }

        private void Save()
        {
            lock (_fileLock)
            {
                var tmp = _cache.ToDictionary(k => k.Key, v => v.Value);

                var json = JsonSerializer.Serialize(
                    tmp,
                    new JsonSerializerOptions { WriteIndented = true }
                );

                File.WriteAllText(_cacheFilePath, json);
            }
        }
    }
}
