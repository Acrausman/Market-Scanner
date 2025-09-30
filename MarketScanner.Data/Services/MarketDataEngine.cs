using System;
using System.Collections.Generic;
using System.Linq;
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

namespace MarketScanner.Data.Models
{
    public class MarketDataEngine
    {
        public event Action<string, double>? OnNewPrice;
        public event Action<string, double>? OnNewRSI;
        public event Action<string, double, double, double>? OnNewSMA; // SMA14, Upper, Lower
        public event Action<string, double>? OnNewVolume;
        public event Action<MarketScanner.Data.Services.TriggerHit>? OnTrigger;

        private System.Timers.Timer timer;
        private Random random;
        private Dictionary<string, double> lastPrices;
        private Dictionary<string, List<double>> priceHistory;
        private Dictionary<string, List<double>> volumeHistory;

        private int rsiPeriod = 14;
        private int smaPeriod = 14;

        public List<string> Symbols { get; private set; }

        public MarketDataEngine(List<string> symbols)
        {
            Symbols = symbols;
            lastPrices = new Dictionary<string, double>();
            priceHistory = new Dictionary<string, List<double>>();
            volumeHistory = new Dictionary<string, List<double>>();
            random = new Random();

            foreach (var s in Symbols)
            {
                lastPrices[s] = 100.0 + random.NextDouble() * 50;
                priceHistory[s] = new List<double>();
                volumeHistory[s] = new List<double>();
            }

            timer = new System.Timers.Timer(1000); // update every second
            timer.Elapsed += Timer_Elapsed;
        }

        public void Start() => timer.Start();
        public void Stop() => timer.Stop();

        private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            foreach (var s in Symbols)
            {
                double change = (random.NextDouble() - 0.5) * 2; // -1 to +1
                lastPrices[s] += change;
                double price = lastPrices[s];
                double volume = 100000 + random.Next(0, 5000);

                priceHistory[s].Add(price);
                volumeHistory[s].Add(volume);

                OnNewPrice?.Invoke(s, price);
                OnNewVolume?.Invoke(s, volume);

                // RSI
                if (priceHistory[s].Count > rsiPeriod)
                {
                    double rsi = CalculateRSI(priceHistory[s]);
                    OnNewRSI?.Invoke(s, rsi);
                }

                // SMA + Bands
                if (priceHistory[s].Count >= smaPeriod)
                {
                    double sma = priceHistory[s]
                        .Skip(priceHistory[s].Count - smaPeriod).Average();
                    double sd = StdDev(priceHistory[s]
                        .Skip(priceHistory[s].Count - smaPeriod).ToList());
                    double upper = sma + 2 * sd;
                    double lower = sma - 2 * sd;

                    OnNewSMA?.Invoke(s, sma, upper, lower);
                }

                // Dummy trigger
                if (price % 20 < 1)
                {
                    OnTrigger?.Invoke(new MarketScanner.Data.Services.TriggerHit
                    {
                        Symbol = s,
                        TriggerName = price % 40 < 20 ? "TrendLong" : "MeanRevertLong",
                        Price = price
                    });
                }
            }
        }

        private double CalculateRSI(List<double> closes)
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