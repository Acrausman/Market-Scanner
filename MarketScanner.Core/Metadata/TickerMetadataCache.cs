using MarketScanner.Core.Models;
using Newtonsoft.Json;
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
            /*
            if (found)
                Console.WriteLine($"[META READ] {symbol} → country={info?.Country}, sector={info?.Sector}");
            else
                Console.WriteLine($"[META READ] {symbol} not found in cache");
            */

            return found;
        }

        public IReadOnlyList<string> GetAllSectors()
        {
            return _cache.Values
                .Select(v => v.Sector)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .OrderBy(s => s)
                .ToList();
        }
        public IReadOnlyList<string> GetAllCountries()
        {
            return _cache.Values
                .Select(v => v.Country)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .OrderBy (s => s)
                .ToList();
        }
        public void AddOrUpdate(TickerInfo info)
        {
            if (info == null || string.IsNullOrWhiteSpace(info.Symbol))
                return;
            lock(_fileLock)
            {
                _cache[info.Symbol] = info;
                Save();
                //Console.WriteLine($"[META WRITE] {info.Symbol} - country={info.Country}, sector={info.Sector}");

            }

        }

        public void SaveCacheToDisk()
        {
            Save();
        }


        private void Load()
        {
            Console.WriteLine("[META DEBUG] Loading metadata cache...");
            Console.WriteLine($"[META DEBUG] Path = {_cacheFilePath}");

            if (!File.Exists(_cacheFilePath))
            {
                Console.WriteLine("[META DEBUG] Cache file not found.");
                return;
            }

            try
            {
                var json = File.ReadAllText(_cacheFilePath);
                var list = JsonConvert.DeserializeObject<List<TickerInfo>>(json)
                            ?? new List<TickerInfo>();

                lock(_fileLock)
                {
                    _cache.Clear();
                    foreach (var info in list)
                    {
                        if(info != null && !string.IsNullOrWhiteSpace(info.Symbol))
                        {
                            _cache[info.Symbol] = info;
                        }
                    }
                    Console.WriteLine($"[META DEBUG] Loaded {_cache.Count} entries from cache.");
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"[META DEBUG] Failed to read cache: {ex}");
            }
        }

        private void Save()
        {
            lock (_fileLock)
                lock (_fileLock)
                {
                    try
                    {
                        Console.WriteLine($"[META DEBUG] Saving metadata cache with {_cache.Count} entries...");

                        var list = _cache.Values.ToList();
                        var json = JsonConvert.SerializeObject(list, Formatting.Indented);

                        File.WriteAllText(_cacheFilePath, json);

                        Console.WriteLine($"[META DEBUG] Saved metadata cache to {_cacheFilePath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[META DEBUG] Failed to write cache: {ex}");
                    }
                }
        }
    }
}
