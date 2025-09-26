using System;
using System.Collections.Generic;
using System.Timers;
using System.Linq;

namespace MarketScanner.UI
{
    public class TriggerHit
    {
        public string Symbol { get; set; }
        public string TriggerName { get; set; } // "TrendLong" or "MeanRevertLong"
        public double Price { get; set; }
    }

    public class MarketDataEngine
    {
        public event Action<string, double> OnNewPrice;
        public event Action<string, double> OnNewRSI;
        public event Action<TriggerHit> OnTrigger;

        private Timer timer;
        private Random random;
        private Dictionary<string, double> lastPrices;
        private Dictionary<string, List<double>> priceHistory;

        public List<string> Symbols { get; private set; }
        private int rsiPeriod = 14;

        public MarketDataEngine(List<string> symbols)
        {
            Symbols = symbols;
            lastPrices = new Dictionary<string, double>();
            priceHistory = new Dictionary<string, List<double>>();
            random = new Random();

            foreach (var s in Symbols)
            {
                lastPrices[s] = 100.0 + random.NextDouble() * 50;
                priceHistory[s] = new List<double>();
            }

            timer = new Timer(1000);// 1 second
            timer.Elapsed += Timer_Elapsed;

        }

        public void Start() => timer.Start();
        public void Stop() => timer.Stop();

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            foreach (var s in Symbols)
            {
                double change = (random.NextDouble() - 0.5) * 2; // -1 to +1
                lastPrices[s] += change;
                double price = lastPrices[s];
                priceHistory[s].Add(price);

                OnNewPrice?.Invoke(s, price);

                //Calculate RSI if enough data
                if (priceHistory[s].Count > rsiPeriod)
                {
                    double rsi = CalculateRSI(priceHistory[s]);
                    OnNewRSI?.Invoke(s, rsi);
                }

                // Dummy triggers
                if (price % 20 < 1) // arbitrary trigger condition
                {
                    OnTrigger?.Invoke(new TriggerHit
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
            for(int i = closes.Count - rsiPeriod; i < closes.Count; i++)
            {
                double delta = closes[i] - closes[i - 1];
                if (delta > 0) gain += delta;
                else loss -= delta;
            }
            double rs = loss == 0 ? 100 : gain / loss;
            return 100 - (100 / (1 + rs));
        }
    
    }

}
