using MarketScanner.Data.Models;
using MarketScanner.Data.Providers;
using MarketScanner.Data.Services.Indicators;
using MarketScanner.UI.Wpf.Services;
using OxyPlot;
using OxyPlot.Axes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;

namespace MarketScanner.UI.Wpf.ViewModels
{
    public class ChartViewModel : INotifyPropertyChanged
    {
        private readonly ChartManager _chartManager;
        private readonly IMarketDataProvider _provider;

        public PlotModel PriceView => _chartManager.PriceView;
        public PlotModel RsiView => _chartManager.RsiView;
        public PlotModel VolumeView => _chartManager.VolumeView;

        private string _priceText;
        public string PriceText { get => _priceText; set { _priceText = value; OnPropertyChanged(); } }

        private string _smaText;
        public string SmaText { get => _smaText; set { _smaText = value; OnPropertyChanged(); } }

        private string _rsiText;
        public string RsiText { get => _rsiText; set { _rsiText = value; OnPropertyChanged(); } }

        private string _volumeText;
        public string VolumeText { get => _volumeText; set { _volumeText = value; OnPropertyChanged(); } }

        public ChartViewModel(IMarketDataProvider provider, ChartManager chartManager)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _chartManager = chartManager ?? throw new ArgumentNullException(nameof(chartManager));
        }

        public void Clear()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _chartManager.ClearAllSeries();
            });
        }

        public async void LoadChartForSymbol(string symbol)
        {
            if (_provider == null)
            {
                Console.WriteLine("[Chart] Market data provider not initialized.");
                return;
            }
            if (_chartManager == null)
            {
                Console.WriteLine("[Chart] ChartManager not initialized.");
                return;
            }
            if (string.IsNullOrWhiteSpace(symbol))
                return;

            var bars = await _provider.GetHistoricalBarsAsync(symbol, 125);
            if (bars == null || bars.Count == 0)
            {
                Console.WriteLine($"[Chart] No bar data for {symbol}");
                return;
            }

            _chartManager.ClearAllSeries();

            if (bars == null || bars.Count == 0)
                return;

            var pricePoints = bars.Select(b => new DataPoint(DateTimeAxis.ToDouble(b.Timestamp), b.Close)).ToList();

            var smaPoints = new List<DataPoint>();
            var upperBand = new List<DataPoint>();
            var lowerBand = new List<DataPoint>();

            const int window = 14;
            for (int i = window - 1; i < bars.Count; i++)
            {
                var slice = bars.Skip(i - window + 1).Take(window).Select(b => b.Close).ToList();
                double sma = slice.Average();
                double std = Math.Sqrt(slice.Average(v => Math.Pow(v - sma, 2)));
                smaPoints.Add(new DataPoint(DateTimeAxis.ToDouble(bars[i].Timestamp), sma));
                upperBand.Add(new DataPoint(DateTimeAxis.ToDouble(bars[i].Timestamp), sma + 2 * std));
                lowerBand.Add(new DataPoint(DateTimeAxis.ToDouble(bars[i].Timestamp), sma - 2 * std));
                
            }

            _chartManager.UpdatePriceData(pricePoints, smaPoints,
                upperBand.Zip(lowerBand, (u, l) => (u, l)).ToList());


            var closes = bars.Select(b => b.Close).ToList();
            var rsiPoints = new List<DataPoint>();

            for (int i = window; i < closes.Count; i++)
            {
                double rsi = MarketScanner.Data.Services.Indicators.RsiCalculator.Calculate(closes.Take(i + 1).ToList(), window);
                var time = DateTimeAxis.ToDouble(bars[i].Timestamp);
                if (!double.IsNaN(rsi))
                    rsiPoints.Add(new DataPoint(time, rsi));
            }

            _chartManager.UpdateRsiData(rsiPoints);

            var volumePoints = bars.Select(b => new DataPoint(DateTimeAxis.ToDouble(b.Timestamp), b.Volume)).ToList();
            _chartManager.UpdateVolumeData(volumePoints);
        }
        public void Update(EquityScanResult result)
        {
            // Ignore invalid or zeroed updates
            if (result.Price <= 0 || double.IsNaN(result.Price)) return;
            if (double.IsNaN(result.SMA) || double.IsNaN(result.RSI)) return;

            Console.WriteLine($"[Chart.Update] {result.Symbol}: Price={result.Price:F2}, SMA={result.SMA:F2}, RSI={result.RSI:F2}");

            var x = OxyPlot.Axes.DateTimeAxis.ToDouble(result.TimeStamp);
            _chartManager.UpdatePriceData(
                new List<DataPoint> { new(x, result.Price) },
                new List<DataPoint> { new(x, result.SMA) },
                new List<(DataPoint, DataPoint)> { (new(x, result.Upper), new(x, result.Lower)) });

            _chartManager.UpdateRsiData(new List<DataPoint> { new(x, result.RSI) });

            PriceText = $"Price: {result.Price:F2}";
            SmaText = $"SMA: {result.SMA:F2}";
            RsiText = $"RSI: {result.RSI:F2}";
            VolumeText = $"Vol: {result.Volume:N0}";
        }

        public async Task LoadHistoricalAsync(string symbol, IMarketDataProvider provider)
        {
            Console.WriteLine($"[Chart] Loading history for {symbol}");
            var closes = await provider.GetHistoricalClosesAsync(symbol, 30);
            var timestamps = await provider.GetHistoricalTimestampsAsync(symbol, 30);

            if (closes == null || timestamps == null || closes.Count == 0)
            {
                Console.WriteLine($"[Chart] No data for {symbol}");
                return;
            }

            const int period = 14;
            var pricePoints = new List<DataPoint>();
            var smaPoints = new List<DataPoint>();
            var bollinger = new List<(DataPoint, DataPoint)>();
            var rsiPoints = new List<DataPoint>();

            for (int i = 0; i < closes.Count && i < timestamps.Count; i++)
            {
                double x = OxyPlot.Axes.DateTimeAxis.ToDouble(timestamps[i]);
                double close = closes[i];
                pricePoints.Add(new DataPoint(x, close));

                if (i >= period)
                {
                    var window = closes.GetRange(i - period, period);
                    double sma = window.Average();
                    double sd = Math.Sqrt(window.Sum(v => Math.Pow(v - sma, 2)) / period);
                    double upper = sma + 2 * sd;
                    double lower = sma - 2 * sd;

                    smaPoints.Add(new DataPoint(x, sma));
                    bollinger.Add((new DataPoint(x, upper), new DataPoint(x, lower)));

                    double rsi = RsiCalculator.Calculate(window);
                    if (!double.IsNaN(rsi))
                        rsiPoints.Add(new DataPoint(x, rsi));
                }
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                _chartManager.ClearAllSeries();
                _chartManager.UpdatePriceData(pricePoints, smaPoints, bollinger, isLive: false);
                _chartManager.UpdateRsiData(rsiPoints);
            });

            Console.WriteLine($"[Chart] {symbol} plotted {pricePoints.Count} pts");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
