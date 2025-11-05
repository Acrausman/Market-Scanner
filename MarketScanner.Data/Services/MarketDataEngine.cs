using MarketScanner.Data.Diagnostics;
using MarketScanner.Core.Models;
using MarketScanner.Core.Configuration;
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
        private readonly AppSettings _settings;
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
        public MarketDataEngine(List<string> symbols, IMarketDataProvider provider, AppSettings settings)
        {
            _settings = settings;

            Symbols = symbols;
            _provider = provider;

            _timer = new System.Timers.Timer(5000);
            _timer.Elapsed += async (s, e) => await TimerElapsed();
        }

        // 🔹 New: Single-symbol constructor
        public MarketDataEngine(IMarketDataProvider provider, AppSettings settings)
        {
            _settings = settings;

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
                await semaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    var (price, volume) = await _provider.GetQuoteAsync(symbol).ConfigureAwait(false);
                    var closes = await LoadRecentClosesAsync(symbol, Math.Max(rsiPeriod, smaPeriod)).ConfigureAwait(false);
                    if (closes.Count < Math.Max(rsiPeriod, smaPeriod))
                    {
                        return;
                    }

                    double rsi = RsiCalculator.Calculate(closes, rsiPeriod, _settings.RsiMethod);
                    var (sma, upper, lower) = BollingerBandsCalculator.Calculate(closes, smaPeriod);

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
                    Logger.Warn($"[Scanner] {symbol}: {ex.Message}");
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        // SINGLE SYMBOL STREAM 
        public void StartSymbol(string symbol)
        {
            StopSymbol(); // cancel previous stream

            _liveCts = new CancellationTokenSource();
            var token = _liveCts.Token;

            _liveTask = Task.Run(async () =>
            {
                Logger.Info($"[Live] Started stream for {symbol}");
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var (price, volume) = await _provider.GetQuoteAsync(symbol).ConfigureAwait(false);

                        if (!double.IsNaN(price))
                            OnNewPrice?.Invoke(symbol, price);

                        if (!double.IsNaN(volume))
                            OnNewVolume?.Invoke(symbol, volume);

                        var closes = await LoadRecentClosesAsync(symbol, Math.Max(rsiPeriod, smaPeriod)).ConfigureAwait(false);
                        if (closes.Count >= rsiPeriod)
                        {
                            double rsi = RsiCalculator.Calculate(closes, rsiPeriod, _settings.RsiMethod);
                            OnNewRSI?.Invoke(symbol, rsi);

                            var (sma, upper, lower) = BollingerBandsCalculator.Calculate(closes, smaPeriod);
                            OnNewSMA?.Invoke(symbol, sma, upper, lower);
                        }

                        await Task.Delay(10000, token).ConfigureAwait(false); // refresh every 10s
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"[Live] {symbol}: {ex.Message}");
                        await Task.Delay(5000, token).ConfigureAwait(false);
                    }
                }

                Logger.Info($"[Live] Stream stopped for {symbol}");
            }, token);
        }

        public void StopSymbol()
        {
            if (_liveCts == null) return;

            Logger.Info("[Live] Stopping previous stream...");
            _liveCts.Cancel();
            _liveCts = null;
            _liveTask = null;
        }

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
            if (_lastRSI.TryGetValue(symbol, out var v))
                return v;
            return null;
        }

        public double? GetLastVolume(string symbol)
        {
            if (_lastVolumes.TryGetValue(symbol, out var v))
                return v;
            return null;
        }


        private async Task<List<double>> LoadRecentClosesAsync(string symbol, int lookback)
        {
            var end = DateTime.UtcNow;
            var start = end.AddDays(-(lookback + 100));
            var bars = await _provider.GetHistoricalBarsAsync(symbol, start, end).ConfigureAwait(false);
            return bars.Select(b => b.Close).ToList();
        }
    }
}
