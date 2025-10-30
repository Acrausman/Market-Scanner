using MarketScanner.Data.Models;
using MarketScanner.Data.Providers;
using MarketScanner.Data.Services.Indicators;
using MarketScanner.Data.Diagnostics;
using MarketScanner.UI.Wpf.Services;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace MarketScanner.UI.Wpf.ViewModels
{
    public class ChartViewModel : INotifyPropertyChanged
    {
        private readonly IMarketDataProvider _provider;
        private readonly IChartService _chartService;
        private readonly Dispatcher _dispatcher;
        private CancellationTokenSource? _loadCts;

        private string _currentSymbol;
        public string CurrentSymbol
        {
            get => _currentSymbol;
            private set => _currentSymbol = value;
        }

        public PlotModel PriceView => _chartService.PriceView;
        public PlotModel RsiView => _chartService.RsiView;
        public PlotModel VolumeView => _chartService.VolumeView;

        private string _priceText = string.Empty;
        public string PriceText
        {
            get => _priceText;
            private set => SetProperty(ref _priceText, value);
        }

        private string _smaText = string.Empty;
        public string SmaText
        {
            get => _smaText;
            private set => SetProperty(ref _smaText, value);
        }

        private string _rsiText = string.Empty;
        public string RsiText
        {
            get => _rsiText;
            private set => SetProperty(ref _rsiText, value);
        }

        private string _volumeText = string.Empty;
        public string VolumeText
        {
            get => _volumeText;
            private set => SetProperty(ref _volumeText, value);
        }

        private string _selectedTimespan = "3M";
        public string SelectedTimespan
        {
            get => _selectedTimespan;
            set
            {
                if(SetProperty(ref _selectedTimespan, value))
                {
                    SetTimespan(value);
                }
            }
        }

        public ChartViewModel(IMarketDataProvider provider, IChartService chartService, Dispatcher? dispatcher = null)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _chartService = chartService ?? throw new ArgumentNullException(nameof(chartService));
            _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
        }

        public void Clear()
        {
            _dispatcher.Invoke(() =>
            {
                _chartService.ClearAllSeries();
                PriceText = string.Empty;
                SmaText = string.Empty;
                RsiText = string.Empty;
                VolumeText = string.Empty;
            });
        }

        public async Task LoadChartForSymbol(string symbol, int? lookbackOverrideDays = null)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return;

            CurrentSymbol = symbol; // <- remember last viewed symbol

            try
            {
                // choose how many days of data to show
                int daysToFetch;
                if (lookbackOverrideDays.HasValue)
                {
                    daysToFetch = lookbackOverrideDays.Value;
                }
                else
                {
                    // fallback default if none provided
                    daysToFetch = 125;
                }

                // 1) get bars (ideally use your caching method if you added one)
                var bars = await _provider.GetHistoricalBarsAsync(symbol, daysToFetch).ConfigureAwait(false);
                if (bars == null || bars.Count == 0)
                    return;

                // 2) build price series, SMA, Bollinger, RSI, Volume
                // (this is basically what you already implemented — keep that logic)
                var closes = bars.Select(b => b.Close).ToList();

                // price
                var pricePoints = bars
                    .Select(b => new DataPoint(DateTimeAxis.ToDouble(b.Timestamp), b.Close))
                    .ToList();

                // SMA(14)
                var smaPoints = closes
                    .Select((_, i) =>
                    {
                        if (i < 13) return new DataPoint(DateTimeAxis.ToDouble(bars[i].Timestamp), double.NaN);
                        var avg14 = closes.Skip(i - 13).Take(14).Average();
                        return new DataPoint(DateTimeAxis.ToDouble(bars[i].Timestamp), avg14);
                    })
                    .ToList();

                // Bollinger(20, 2σ)
                var bollBands = new List<(DataPoint upper, DataPoint lower)>();
                for (int i = 19; i < closes.Count; i++)
                {
                    var slice = closes.Skip(i - 19).Take(20).ToList();
                    var mean = slice.Average();
                    var sd = Math.Sqrt(slice.Sum(v => Math.Pow(v - mean, 2)) / slice.Count);
                    var up = mean + 2 * sd;
                    var dn = mean - 2 * sd;
                    var ts = DateTimeAxis.ToDouble(bars[i].Timestamp);
                    bollBands.Add((new DataPoint(ts, up), new DataPoint(ts, dn)));
                }

                // RSI: if your RsiCalculator returns a single latest RSI:
                var latestRsi = RsiCalculator.Calculate(closes, 14);
                var rsiPoints = bars
                    .Select(b => new DataPoint(DateTimeAxis.ToDouble(b.Timestamp), latestRsi))
                    .ToList();
                Console.WriteLine($"RSI values for {symbol}: {string.Join(", ", rsiPoints.TakeLast(5))}");


                // Volume
                var volumePoints = bars
                    .Select(b => new DataPoint(DateTimeAxis.ToDouble(b.Timestamp), b.Volume))
                    .ToList();

                // 3) push into charts on UI thread
                App.Current.Dispatcher.Invoke(() =>
                {
                    _chartService.UpdatePriceData(pricePoints, smaPoints, bollBands);
                    _chartService.UpdateRsiData(rsiPoints);
                    _chartService.UpdateVolumeData(volumePoints);
                });
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"[Chart] Error loading {symbol}: {ex.Message}");
            }
        }

        public void SetTimespan(string span)
        {
            if (string.IsNullOrWhiteSpace(CurrentSymbol))
                return; // nothing selected yet

            int days = span switch
            {
                "1M" => 22,      // ~22 trading days
                "3M" => 66,
                "6M" => 132,
                "1Y" => 252,
                "YTD" => (int)(DateTime.Today.DayOfYear * 0.7), // rough trading days YTD
                "Max" => 1000,
                _ => 125
            };

            // fire and forget; we don't await so UI doesn't freeze
            _ = LoadChartForSymbol(CurrentSymbol, days);
        }


        public void Update(EquityScanResult result)
        {
            if (result == null)
            {
                return;
            }

            if (double.IsNaN(result.Price) || result.Price <= 0 ||
                double.IsNaN(result.SMA) || double.IsNaN(result.RSI))
            {
                return;
            }

            _dispatcher.InvokeAsync(() =>
            {
                var time = DateTimeAxis.ToDouble(result.TimeStamp);
                var pricePoint = new DataPoint(time, result.Price);
                var smaPoint = new DataPoint(time, result.SMA);
                var upper = new DataPoint(time, result.Upper);
                var lower = new DataPoint(time, result.Lower);
                var rsiPoint = new DataPoint(time, result.RSI);
                var volumePoint = new DataPoint(time, result.Volume);

                _chartService.UpdatePriceData(new[] { pricePoint }, new[] { smaPoint }, new[] { (upper, lower) }, isLive: true);
                _chartService.UpdateRsiData(new[] { rsiPoint });
                _chartService.UpdateVolumeData(new[] { volumePoint });

                PriceText = $"Price: {result.Price:F2}";
                SmaText = $"SMA: {result.SMA:F2}";
                RsiText = $"RSI: {result.RSI:F2}";
                VolumeText = $"Vol: {result.Volume:N0}";
            }, DispatcherPriority.Background);
        }

        private void CancelOngoingLoad()
        {
            if (_loadCts == null)
            {
                return;
            }

            _loadCts.Cancel();
            _loadCts.Dispose();
            _loadCts = null;
        }

        private static List<DataPoint> CreatePriceSeries(IReadOnlyList<Bar> bars)
            => bars.Select(b => new DataPoint(DateTimeAxis.ToDouble(b.Timestamp), b.Close)).ToList();

        private static List<DataPoint> CreateVolumeSeries(IReadOnlyList<Bar> bars)
            => bars.Select(b => new DataPoint(DateTimeAxis.ToDouble(b.Timestamp), b.Volume)).ToList();

        private static List<DataPoint> CreateRsiSeries(IReadOnlyList<Bar> bars, int period = 14)
        {
            var closes = bars.Select(b => b.Close).ToList();
            var rsiPoints = new List<DataPoint>();

            for (int i = period; i < closes.Count; i++)
            {
                var slice = closes.Take(i + 1).ToList();
                double rsi = RsiCalculator.Calculate(slice, period);
                if (double.IsNaN(rsi))
                {
                    continue;
                }

                var time = DateTimeAxis.ToDouble(bars[i].Timestamp);
                rsiPoints.Add(new DataPoint(time, rsi));
            }

            return rsiPoints;
        }

        private static List<DataPoint> CreateSmaSeries(IReadOnlyList<Bar> bars, out List<(DataPoint upper, DataPoint lower)> bands, int period = 14)
        {
            var smaPoints = new List<DataPoint>();
            bands = new List<(DataPoint upper, DataPoint lower)>();

            for (int i = period - 1; i < bars.Count; i++)
            {
                var window = bars.Skip(i - period + 1).Take(period).Select(b => b.Close).ToList();
                double sma = window.Average();
                double std = Math.Sqrt(window.Average(v => Math.Pow(v - sma, 2)));
                double time = DateTimeAxis.ToDouble(bars[i].Timestamp);

                smaPoints.Add(new DataPoint(time, sma));
                bands.Add((new DataPoint(time, sma + 2 * std), new DataPoint(time, sma - 2 * std)));
            }

            return smaPoints;
        }

        private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
            {
                return false;
            }

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
