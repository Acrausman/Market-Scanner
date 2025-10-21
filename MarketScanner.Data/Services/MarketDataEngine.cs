using MarketScanner.Data.Models;
using MarketScanner.Data.Models.MarketScanner.Data.Models;
using MarketScanner.Data.Providers;
using MarketScanner.Data.Services.Indicators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace MarketScanner.Data
{
    public class MarketDataEngine
    {
        private readonly IMarketDataProvider _provider;
        private System.Timers.Timer _timer;

        public List<string> Symbols { get; }

        // 🔹 Event hooks
        public event Action<string, double>? OnNewPrice;
        public event Action<string, double>? OnNewRSI;
        public event Action<string, double, double, double>? OnNewSMA;
        public event Action<string, double>? OnNewVolume;
        public event Action<EquityScanResult>? OnEquityScanned;

        private readonly Dictionary<string, double> _lastPrices = new();
        private readonly Dictionary<string, double> _lastVolumes = new();
        private readonly Dictionary<string, double> _lastRSI = new();
        private readonly Dictionary<string, (double Sma, double Upper, double Lower)> _lastSMA = new();

        private bool _isPaused;
        private readonly int rsiPeriod = 14;
        private readonly int smaPeriod = 14;

        // 🔹 Single-symbol live stream support
        private CancellationTokenSource? _liveCts;
        private Task? _liveTask;

        // 🔹 Constructors
        public MarketDataEngine(List<string> symbols, IMarketDataProvider provider)
        {
            Symbols = symbols;
            _provider = provider;

            _timer = new System.Timers.Timer(5000);
            _timer.Elapsed += async (s, e) => await TimerElapsed();
        }

        // 🔹 New: Single-symbol constructor
        public MarketDataEngine(IMarketDataProvider provider)
        {
            Symbols = new List<string>();
            _provider = provider;

            _timer = new System.Timers.Timer(5000);
            _timer.Elapsed += async (s, e) => await TimerElapsed();
        }

        public void Start() => _timer.Start();
        public void Stop() => _timer.Stop();
        public void Pause() => _isPaused = true;
        public void Resume() => _isPaused = false;

        // =====================================================
        // =============== MULTI-SYMBOL SCANNER =================
        // =====================================================
        private async Task TimerElapsed()
        {
            if (_isPaused || Symbols.Count == 0) return;

            var semaphore = new SemaphoreSlim(5);
            var tasks = Symbols.Select(async symbol =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var (price, volume) = await _provider.GetQuoteAsync(symbol);
                    var closes = await _provider.GetHistoricalClosesAsync(symbol, 120);
                    var timestamps = await _provider.GetHistoricalTimestampsAsync(symbol, 50);

                    if (closes.Count < 14) return;

                    double rsi = RsiCalculator.Calculate(closes);
                    double sma = closes.TakeLast(smaPeriod).Average();
                    double sd = StdDev(closes.TakeLast(smaPeriod).ToList());
                    double upper = sma + 2 * sd;
                    double lower = sma - 2 * sd;

                    _lastPrices[symbol] = price;
                    _lastVolumes[symbol] = volume;
                    _lastRSI[symbol] = rsi;
                    _lastSMA[symbol] = (sma, upper, lower);

                    OnNewPrice?.Invoke(symbol, price);
                    OnNewVolume?.Invoke(symbol, volume);
                    OnNewRSI?.Invoke(symbol, rsi);
                    OnNewSMA?.Invoke(symbol, sma, upper, lower);

                    OnEquityScanned?.Invoke(new EquityScanResult
                    {
                        Symbol = symbol,
                        Price = price,
                        RSI = rsi,
                        SMA = sma,
                        Upper = upper,
                        Lower = lower,
                        Volume = volume,
                        TimeStamp = DateTime.Now
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Scanner] {symbol}: {ex.Message}");
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        // =====================================================
        // =============== SINGLE SYMBOL STREAM ================
        // =====================================================
        public void StartSymbol(string symbol)
        {
            StopSymbol(); // cancel previous stream

            _liveCts = new CancellationTokenSource();
            var token = _liveCts.Token;

            _liveTask = Task.Run(async () =>
            {
                Console.WriteLine($"[Live] Started stream for {symbol}");
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var (price, volume) = await _provider.GetQuoteAsync(symbol);

                        if (!double.IsNaN(price))
                            OnNewPrice?.Invoke(symbol, price);

                        if (!double.IsNaN(volume))
                            OnNewVolume?.Invoke(symbol, volume);

                        var closes = await _provider.GetHistoricalClosesAsync(symbol, 120);
                        if (closes.Count >= 14)
                        {
                            double rsi = RsiCalculator.Calculate(closes);
                            OnNewRSI?.Invoke(symbol, rsi);

                            var recent = closes.TakeLast(smaPeriod).ToList();
                            double sma = recent.Average();
                            double sd = StdDev(recent);
                            OnNewSMA?.Invoke(symbol, sma, sma + 2 * sd, sma - 2 * sd);
                        }

                        await Task.Delay(10000, token); // refresh every 10s
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Live] {symbol}: {ex.Message}");
                        await Task.Delay(5000, token);
                    }
                }

                Console.WriteLine($"[Live] Stream stopped for {symbol}");
            }, token);
        }

        public void StopSymbol()
        {
            if (_liveCts == null) return;

            Console.WriteLine("[Live] Stopping previous stream...");
            _liveCts.Cancel();
            _liveCts = null;
            _liveTask = null;
        }

        // =====================================================
        // =============== HELPER FUNCTIONS ===================
        // =====================================================

        /*
        public double CalculateRSI(List<double> closes, int period = 14)
        {
            if (closes == null || closes.Count <= period)
                return double.NaN;

            double gain = 0, loss = 0;

            // initialize with first 'period' differences
            for (int i = 1; i <= period; i++)
            {
                double diff = closes[i] - closes[i - 1];
                if (diff >= 0)
                    gain += diff;
                else
                    loss -= diff;
            }

            double avgGain = gain / period;
            double avgLoss = loss / period;

            // Wilder's smoothing formula
            for (int i = period + 1; i < closes.Count; i++)
            {
                double diff = closes[i] - closes[i - 1];
                if (diff >= 0)
                {
                    avgGain = ((avgGain * (period - 1)) + diff) / period;
                    avgLoss = ((avgLoss * (period - 1)) + 0) / period;
                }
                else
                {
                    avgGain = ((avgGain * (period - 1)) + 0) / period;
                    avgLoss = ((avgLoss * (period - 1)) - diff) / period;
                }
            }

            if (avgLoss == 0) return 100;

            double rs = avgGain / avgLoss;
            return 100 - (100 / (1 + rs));
        }
        */

        public double? GetLastPrice(string symbol)
        {
            if (_lastPrices.TryGetValue(symbol, out var v))
                return v;
            return null;
        }

        public (double Sma, double Upper, double Lower)? GetLastSma(string symbol)
        {
            if (_lastSMA.TryGetValue(symbol, out var s))
                return s;
            return null;
        }

        public double? GetLastRSI(string symbol)
        {
            if (_lastPrices.TryGetValue(symbol, out var v))
                return v;
            return null;
        }

        public double? GetLastVolume(string symbol)
        {
            if (_lastVolumes.TryGetValue(symbol, out var v))
                return v;
            return null;
        }


        public double StdDev(List<double> values)
        {
            if (values.Count <= 1) return 0;
            double avg = values.Average();
            double sum = values.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sum / (values.Count - 1));
        }
    }
}
