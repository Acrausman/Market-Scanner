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

        public async Task LoadChartForSymbolAsync(string symbol, CancellationToken cancellationToken = default)
        {
            CancelOngoingLoad();

            if (string.IsNullOrWhiteSpace(symbol))
            {
                Clear();
                return;
            }

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _loadCts = linkedCts;
            var token = linkedCts.Token;

            try
            {
                var bars = await _provider.GetHistoricalBarsAsync(symbol, 150).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();

                if (bars == null || bars.Count == 0)
                {
                    Logger.WriteLine($"[Chart] No bar data for {symbol}");
                    await _dispatcher.InvokeAsync(() =>
                    {
                        _chartService.ClearAllSeries();
                        PriceText = "";
                        SmaText = "";
                        RsiText = "";
                        VolumeText = "";
                    });
                    return;
                }

                var pricePoints = CreatePriceSeries(bars);
                var smaPoints = CreateSmaSeries(bars, out var bollingerBands);
                var rsiPoints = CreateRsiSeries(bars);
                var volumePoints = CreateVolumeSeries(bars);

                token.ThrowIfCancellationRequested();

                await _dispatcher.InvokeAsync(() =>
                {
                    _chartService.UpdatePriceData(pricePoints, smaPoints, bollingerBands, isLive: false);
                    _chartService.UpdateRsiData(rsiPoints);
                    _chartService.UpdateVolumeData(volumePoints);

                    var lastBar = bars[^1];
                    PriceText = $"Price: {lastBar.Close:F2}";
                    SmaText = smaPoints.Count > 0 ? $"SMA: {smaPoints[^1].Y:F2}" : string.Empty;
                    RsiText = rsiPoints.Count > 0 ? $"RSI: {rsiPoints[^1].Y:F2}" : string.Empty;
                    VolumeText = $"Vol: {lastBar.Volume:N0}";
                }, DispatcherPriority.Background);
            }
            catch (OperationCanceledException)
            {
                // ignored - selection changed
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"[Chart] Failed to load {symbol}: {ex.Message}");
                await _dispatcher.InvokeAsync(() =>
                {
                    _chartService.ClearAllSeries();
                    PriceText = string.Empty;
                    SmaText = string.Empty;
                    RsiText = string.Empty;
                    VolumeText = string.Empty;
                });
                throw;
            }
            finally
            {
                linkedCts.Dispose();
                if (ReferenceEquals(_loadCts, linkedCts))
                {
                    _loadCts = null;
                }
            }
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
