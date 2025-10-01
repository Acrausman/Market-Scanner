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
            int maxConcurrency = 5;
            var semaphore = new SemaphoreSlim(maxConcurrency);

            var tasks = Symbols.Select(async s =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var (price, volume) = await _provider.GetQuoteAsync(s);
                    var closes = (await _provider.GetHistoricalClosesAsync(s, 50));


                    // RSI
                    if (closes == null || closes.Count < rsiPeriod)
                    {
                        Console.WriteLine($"Ticker {s} skipped: insufficient historical data.");
                        return;
                    }

                    _lastPrices[s] = price;
                    OnNewPrice?.Invoke(s, price);
                    _lastVolumes[s] = volume;
                    OnNewVolume?.Invoke(s, volume);
                    
                    double rsi = CalculateRSI(closes);
                    _lastRSI[s] = rsi;
                    OnNewRSI?.Invoke(s, rsi);

                    double sma = closes.TakeLast(rsiPeriod).Average();
                    double sd = StdDev(closes.TakeLast(rsiPeriod).ToList());
                    double upper = sma + 2 * sd;
                    double lower = sma - 2 * sd;

                    _LastSMA[s] = (sma,upper,lower);
                    OnNewSMA?.Invoke(s, sma, upper,lower);

                    if (rsi >= 70 || rsi <= 30)
                    {
                        if(rsi >= 70)
                        {
                            OnTrigger?.Invoke(new TriggerHit
                            { 
                                Symbol = s,
                                TriggerName = "Overbought",
                                Price = price
                            
                            });
                        }
                        else if(rsi <= 30)
                        {
                            OnTrigger?.Invoke(new TriggerHit
                            { 
                                Symbol = s,
                                TriggerName = "Oversold",
                                Price = price
                            });
                        }

                        OnEquityScanned?.Invoke(new EquityScanResult {
                            Symbol = s,
                            RSI = rsi,
                            Price = price,
                            SMA = sma,
                            Upper = upper,
                            Lower = lower
                        });                           
                    }

                    await Task.Delay(100);

                }
                catch (Exception ex) 
                {
                    //Ignore errors for ticker
                    Console.WriteLine($"Ticker {s} failed: {ex.Message}");
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
        private double CalculateRSI(IReadOnlyList<double> closes)
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

        private double StdDev(List<double> values)
        {
            double avg = values.Average();
            double sum = values.Sum(v => (v - avg) * (v - avg));
            return Math.Sqrt(sum / (values.Count - 1));
        }
    }

}