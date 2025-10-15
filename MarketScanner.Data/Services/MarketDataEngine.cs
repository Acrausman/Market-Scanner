using MarketScanner.Data.Models;
using MarketScanner.Data.Models.MarketScanner.Data.Models;
using MarketScanner.Data.Providers;
using MarketScanner.Data.Services;
using OxyPlot;
using OxyPlot.Axes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Threading;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;


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
        public event Action<List<DataPoint>, List<DataPoint>, List<(DataPoint upper, DataPoint lower)>>? OnPriceDataUpdated;


        //private Random random;
        private Dictionary<string, double> lastPrices = new();
        private Dictionary<string, List<double>> priceHistory = new();
        private Dictionary<string, List<double>> volumeHistory = new();
        private readonly Dictionary<string, List<EquityDataPoint>> _historicalData = new();

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

            var tasks = Symbols.Select(async symbol =>
            {
                await semaphore.WaitAsync();
                try
                {
                    Log($"Starting scan for {symbol}...");

                    var (price, volume) = await _provider.GetQuoteAsync(symbol);
                    var timestamps = await _provider.GetHistoricalTimestampsAsync(symbol, 50);
                    var closes = await _provider.GetHistoricalClosesAsync(symbol, 50);
                    if(closes.Count != timestamps.Count)
                    {
                        Log($"Ticker {symbol} skipped: closes ({closes.Count}) and timestamps ({timestamps.Count}) count mismatch.");
                        return;
                    }

                    var pricePoints = closes
                    .Zip(timestamps, (close,time) => new DataPoint(DateTimeAxis.ToDouble(time), close))
                    .ToList();

                    _lastPrices[symbol] = price;
                    OnNewPrice?.Invoke(symbol, price);
                    Log($"Quote for {symbol}: Price={price}, Volume={volume}");

                    // --- Volume ---
                    if (!double.IsNaN(volume))
                    {
                        _lastVolumes[symbol] = volume;
                        OnNewVolume?.Invoke(symbol, volume);
                    }

                    // --- RSI ---
                    double rsi = CalculateRSI(closes);
                    if (!double.IsNaN(rsi))
                    {
                        _lastRSI[symbol] = rsi;
                        OnNewRSI?.Invoke(symbol, rsi);
                        Log($"RSI for {symbol}: {rsi:F2}");
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

                        _LastSMA[symbol] = (sma, upper, lower);
                        OnNewSMA?.Invoke(symbol, sma, upper, lower);
                        Log($"SMA for {symbol}: {sma:F2}, Upper={upper:F2}, Lower={lower:F2}");
                    }

                    for (int i = 0; i < closes.Count; i++)
                    {
                        double close = closes[i];
                        DateTime ts = i < timestamps.Count ? timestamps[i] : DateTime.Now;

                        AddEquityDataPoint(
                            symbol,
                            close,
                            sma,
                            upper,
                            lower,
                            rsi,
                            volume,
                            ts
                        );
                    }


                    // --- sanity checks ---
                    if (double.IsNaN(price))
                    {
                        Log($"Ticker {symbol} skipped: price is NaN.");
                        return;
                    }

                    if (closes == null || closes.Count == 0 || closes.Count < rsiPeriod || closes.All(c => double.IsNaN(c) || c <= 0))
                    {
                        Log($"Ticker {symbol} skipped: insufficient historical data.");
                        return;
                    }

                    // --- Price ---


                    // --- Fire full scan result for UI ---
                    var latestTs = timestamps.LastOrDefault();
                    DateTime finalTs = (latestTs == default ? DateTime.Now : latestTs);

                    // If timestamps are date-only (00:00), use the current time portion
                    if (finalTs.Hour == 0 && finalTs.Minute == 0 && finalTs.Second == 0)
                        finalTs = DateTime.Now;

                    OnEquityScanned?.Invoke(new EquityScanResult
                    {
                        Symbol = symbol,
                        Price = price,
                        RSI = rsi,
                        SMA = sma,
                        Upper = upper,
                        Lower = lower,
                        Volume = volume,
                        TimeStamp = finalTs
                    });


                    // --- Trigger check (e.g., OB/OS) ---
                    if (_lastRSI.ContainsKey(symbol) && (_lastRSI[symbol] >= 70 || _lastRSI[symbol] <= 30))
                    {
                        OnTrigger?.Invoke(new TriggerHit
                        {
                            Symbol = symbol,
                            TriggerName = _lastRSI[symbol] >= 70 ? "Overbought" : "Oversold",
                            Price = price
                        });
                    }

                    await Task.Delay(100); // throttle API requests
                }
                catch (Exception ex)
                {
                    Log($"Ticker {symbol} failed: {ex.Message}");
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }


        public List<EquityDataPoint> GetHistoricalData(string symbol)
        {
            lock (_historicalDataLock)
            {
                if (_historicalData.TryGetValue(symbol, out var list))
                    return list;
                return new List<EquityDataPoint>();
            }
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

        private readonly object _historicalDataLock = new();

        public void AddEquityDataPoint(string symbol, double price, double sma, 
                                        double upper, double lower, double rsi, double volume, 
                                        DateTime timestamp)
        {
            lock (_historicalDataLock)
            {
                if (!_historicalData.ContainsKey(symbol))
                    _historicalData[symbol] = new List<EquityDataPoint>();

                _historicalData[symbol].Add(new EquityDataPoint
                {
                    Timestamp = timestamp,
                    Price = price,
                    SMA = sma,
                    UpperBand = upper,
                    LowerBand = lower,
                    RSI = rsi,
                    Volume = volume
                });

                //Keep only last 500 entrie to limit memory
                if (_historicalData[symbol].Count > 500)
                    _historicalData[symbol].RemoveAt(0);
            }
        }
    }

}