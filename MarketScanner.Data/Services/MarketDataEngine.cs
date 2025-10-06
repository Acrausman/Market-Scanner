using MarketScanner.Data.Models;
using MarketScanner.Data.Providers;
using MarketScanner.Data.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Timers;


namespace MarketScanner.Data.Services
{
    public class TriggerHit
    {
        public required string Symbol { get; set; }
        public required string TriggerName { get; set; } 
        public double Price { get; set; } 
    }
}

namespace MarketScanner.Data
{
    public class MarketDataEngine
    {

        private readonly IMarketDataProvider _provider;
        public IMarketDataProvider Provider => _provider;
        private System.Timers.Timer _timer;

        public List<string> Symbols { get; }

        public event Action<string, double>? OnNewPrice;
        public event Action<string, double>? OnNewRSI;
        public event Action<string, double, double, double>? OnNewSMA; // SMA14, Upper, Lower
        public event Action<string, double>? OnNewVolume;
        public event Action<MarketScanner.Data.Services.TriggerHit>? OnTrigger;
        public event Action<EquityScanResult>? OnEquityScanned;


        //private Random random;
        private Dictionary<string, double> lastPrices = new();
        private Dictionary<string, List<double>> priceHistory = new();
        private Dictionary<string, List<double>> volumeHistory = new();

        private readonly Dictionary<string, double> _lastPrices = new();
        private readonly Dictionary<string, double> _lastVolumes = new();
        private readonly Dictionary<string, double> _lastRSI = new();
        private readonly Dictionary<string, (double Sma, double Upper, double Lower)> _LastSMA = new();

        private bool _isPaused = false;

        public void Pause() => _isPaused = true;
        public void Resume() => _isPaused = false;

        public bool EnableDebugLogging { get; set; } = false;
        private void Log(string message)
        {
            if(EnableDebugLogging) Console.WriteLine(message);
        }

        private int rsiPeriod = 14;
        private int smaPeriod = 14;



        public MarketDataEngine(List<string> symbols, IMarketDataProvider provider)
        {
            Symbols = symbols;
            _provider = provider;

            _timer = new System.Timers.Timer(5000);
            _timer.Elapsed += async (s, e) => await TimerElapsed();

        }

        public void Start() => _timer.Start();
        public void Stop() => _timer.Stop();

        private async Task TimerElapsed()
        {
            if (_isPaused) return;

            int maxConcurrency = 5;
            var semaphore = new SemaphoreSlim(maxConcurrency);

            var tasks = Symbols.Select(async s =>
            {
                await semaphore.WaitAsync();
                try
                {
                   Log($"Starting scan for {s}...");

                    var (price, volume) = await _provider.GetQuoteAsync(s);
                    var closes = await _provider.GetHistoricalClosesAsync(s, 50);

                    // --- sanity checks ---
                    if (double.IsNaN(price))
                    {
                        Log($"Ticker {s} skipped: price is NaN (no valid snapshot or last trade).");
                        return;
                    }

                    if (closes == null || closes.Count == 0)
                    {
                        Log($"Ticker {s} skipped: no historical data returned.");
                        return;
                    }

                    if (closes.Count < rsiPeriod)
                    {
                        Log($"Ticker {s} skipped: only {closes.Count} candles (< {rsiPeriod} required).");
                        return;
                    }

                    if (closes.All(c => double.IsNaN(c) || c <= 0))
                    {
                        Log($"Ticker {s} skipped: all close values are invalid or zero.");
                        return;
                    }


                    // --- Price ---
                    _lastPrices[s] = price;
                    OnNewPrice?.Invoke(s, price);
                    Log($"Quote for {s}: Price={price}, Volume={volume}");

                    // --- Volume ---
                    if (!double.IsNaN(volume))
                    {
                        _lastVolumes[s] = volume;
                        OnNewVolume?.Invoke(s, volume);
                    }

                    // --- RSI ---
                    double rsi = CalculateRSI(closes);
                    if (!double.IsNaN(rsi))
                    {
                        _lastRSI[s] = rsi;
                        OnNewRSI?.Invoke(s, rsi);
                        Log($"RSI for {s}: {rsi:F2}");
                    }

                    // --- SMA + Bollinger ---
                    double sma = double.NaN, upper = double.NaN, lower = double.NaN;
                    var recent = closes.TakeLast(smaPeriod).ToList();
                    if (recent.Count == smaPeriod)
                    {
                        sma = recent.Average();
                        double sd = StdDev(recent);
                        upper = sma + 2 * sd;
                        lower = sma - 2 * sd;

                        _LastSMA[s] = (sma, upper, lower);
                        OnNewSMA?.Invoke(s, sma, upper, lower);

                        Log($"SMA for {s}: {sma:F2}, Upper={upper:F2}, Lower={lower:F2}");
                    }

                    // --- Always send scan result (for chart/UI) ---
                    OnEquityScanned?.Invoke(new EquityScanResult
                    {
                        Symbol = s,
                        RSI = _lastRSI.ContainsKey(s) ? _lastRSI[s] : double.NaN,
                        Price = price,
                        SMA = sma,
                        Upper = upper,
                        Lower = lower,
                        Volume = _lastVolumes.ContainsKey(s) ? _lastVolumes[s] : double.NaN
                    });

                    // --- Only send trigger when OB/OS ---
                    if (_lastRSI.ContainsKey(s) && (_lastRSI[s] >= 70 || _lastRSI[s] <= 30))
                    {
                        OnTrigger?.Invoke(new TriggerHit
                        {
                            Symbol = s,
                            TriggerName = _lastRSI[s] >= 70 ? "Overbought" : "Oversold",
                            Price = price
                        });
                    }

                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    Log($"Ticker {s} failed: {ex.Message}");
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        

        


        public double? GetLastPrice(string symbol) =>
            _lastPrices.TryGetValue(symbol, out var p) ? p : null;

        public double? GetLastVolume(string symbol) =>
            _lastVolumes.TryGetValue(symbol, out var v) ? v : null;

        public double? GetLastRSI(string symbol) =>
            _lastRSI.TryGetValue(symbol, out var r) ? r : null;

        public (double Sma, double Upper, double Lower)? GetLastSma(string symbol) =>
            _LastSMA.TryGetValue(symbol, out var s) ? s : null;
        public double CalculateRSI(IReadOnlyList<double> closes)
        {
            if (closes.Count <= rsiPeriod) return double.NaN;

            double gain = 0, loss = 0;
            for (int i = closes.Count - rsiPeriod + 1; i < closes.Count; i++)
            {
                double delta = closes[i] - closes[i - 1];
                if (delta > 0) gain += delta;
                else loss -= delta;
            }
            double rs = loss == 0 ? 100 : gain / loss;
            return 100 - 100 / (1 + rs);
        }

        public double StdDev(List<double> values)
        {
            double avg = values.Average();
            double sum = values.Sum(v => (v - avg) * (v - avg));
            return Math.Sqrt(sum / (values.Count - 1));
        }
    }

}