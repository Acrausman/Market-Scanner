// Normalized after refactor: updated namespace and using references
using MarketScanner.Core.Models;
using MarketScanner.Core.Configuration;
using MarketScanner.Data.Providers;
using MarketScanner.Core.Indicators;
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
        private readonly AppSettings _settings;
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
            _settings = AppSettings.Load();
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

                var end = DateTime.UtcNow;
                var start = end.AddDays(-daysToFetch);
                var bars = (await _provider.GetHistoricalBarsAsync(symbol, start, end, CancellationToken.None).ConfigureAwait(false)).ToList();
                if(bars == null || bars.Count < 2)
                {
                    Logger.WriteLine($"[Chart] Skipping chart load for {symbol}: insufficient data");
                    return;
                }
                if (bars.Count == 0)
                    return;

                // 2) build price series, SMA, Bollinger, RSI, Volume
                var closes = bars.Select(b => b.Close).ToList();

                // price
                var pricePoints = bars
                    .Select(b => new DataPoint(DateTimeAxis.ToDouble(b.Timestamp), b.Close))
                    .ToList();

                var smaSeries = SmaCalculator.CalculateSeries(closes, 14);
                var smaPoints = BuildAlignedSeries(bars, smaSeries);

                var bollSeries = BollingerBandsCalculator.CalculateSeries(closes, 20);
                var bollBands = BuildAlignedBands(bars, bollSeries);

                var rsiSeries = RsiCalculator.CalculateSeries(closes, 14);
                var rsiPoints = BuildAlignedSeries(bars, rsiSeries);
                //Logger.Debug($"RSI values for {symbol}: {string.Join(", ", rsiPoints.TakeLast(Math.Min(5, rsiPoints.Count)))}");


                // Volume
                var volumePoints = bars
                    .Select(b => new DataPoint(DateTimeAxis.ToDouble(b.Timestamp), b.Volume))
                    .ToList();

                // 3) push into charts on UI thread
                App.Current.Dispatcher.Invoke(() =>
                {
                    _chartService.UpdatePriceData(pricePoints, smaPoints, bollBands);
                    _chartService.UpdateRsiData(rsiPoints);
                    //Logger.WriteLine($"[Chart] {symbol} bars={bars.Count}, smaPoints={smaPoints.Count}, rsiPoints={rsiPoints.Count}, boll={bollBands.Count}");
                    _chartService.UpdateVolumeData(volumePoints);
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"[Chart] Error loading {symbol}: {ex}");
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

        private static List<DataPoint> BuildAlignedSeries(IReadOnlyList<Bar> bars, IReadOnlyList<double> series)
        {
            var points = new List<DataPoint>(series.Count);
            if (bars == null || series == null || bars.Count == 0 || series.Count == 0)
                return points;

            int offset = bars.Count - series.Count;
            if (offset < 0) offset = 0;

            for (int s = 0; s < series.Count; s++)
            {
                int b = s + offset;
                if (b < 0 || b >= bars.Count)
                    continue;

                double value = series[s];
                if (double.IsNaN(value))
                    continue;

                points.Add(new DataPoint(DateTimeAxis.ToDouble(bars[b].Timestamp), value));
            }

            return points;
        }

        private static List<(DataPoint upper, DataPoint lower)> BuildAlignedBands(
            IReadOnlyList<Bar> bars,
            IReadOnlyList<(double Middle, double Upper, double Lower)> series)
        {
            var bands = new List<(DataPoint upper, DataPoint lower)>(series.Count);
            if (bars == null || series == null || bars.Count == 0 || series.Count == 0)
                return bands;

            int offset = bars.Count - series.Count;
            if (offset < 0) offset = 0;

            for (int s = 0; s < series.Count; s++)
            {
                int b = s + offset;
                if (b < 0 || b >= bars.Count)
                    continue;

                var entry = series[s];
                if (double.IsNaN(entry.Upper) || double.IsNaN(entry.Lower))
                    continue;

                double timestamp = DateTimeAxis.ToDouble(bars[b].Timestamp);
                bands.Add((new DataPoint(timestamp, entry.Upper), new DataPoint(timestamp, entry.Lower)));
            }

            return bands;
        }


        public void Update(EquityScanResult result)
        {
            var bars = result.MetaData?.Bars;
            if(bars == null || bars.Count < 2)
            {
                Clear();
                return;
            }
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

_chartService.UpdatePriceData(new[] { pricePoint }, Array.Empty<DataPoint>(), Array.Empty<(DataPoint upper, DataPoint lower)>(), isLive: true);
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
