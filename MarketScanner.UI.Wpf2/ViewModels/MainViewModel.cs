using MarketScanner.Data;
using MarketScanner.Data.Models;
using MarketScanner.Data.Services;
using MarketScanner.UI.Wpf.Managers;
using MarketScanner.UI.Wpf.Services;
using MarketScanner.UI.Wpf.ViewModels;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

public class MainViewModel : INotifyPropertyChanged
{
    #region Fields
    private readonly ChartManager _chartManager;
    private readonly MarketDataEngine _engine;
    private readonly AlertManager _alertManager;
    private readonly AlertService _alertService;
    private readonly EmailService _emailService;
    private readonly PolygonMarketDataProvider _provider;
    private readonly EquityScannerService _scanner;
    private CancellationTokenSource? _cts;
    private string apiKey = "YISIR_KLqJAdX7U6ix6Pjkyx70C_QgpI";

    private List<DataPoint> _pricePoints = new();
    private List<DataPoint> _smaPoints = new();
    private List<(DataPoint upper, DataPoint lower)> _bollingerPoints = new();
    private List<DataPoint> _rsiPoints = new();
    private List<DataPoint> _volumePoints = new();
    private LineSeries _priceSeries = new() { Title = "Price" };
    private LineSeries _smaSeries = new() { Title = "SMA" };
    private LineSeries _upperSeries = new() { Title = "Upper Band" };
    private LineSeries _lowerSeries = new() { Title = "Lower Band" };
    private LineSeries _rsiSeries = new() { Title = "RSI" };
    private RectangleBarSeries _volumeSeries = new() { Title = "Volume" };



    private int _progressValue;
    public int ProgressValue
    {
        get => _progressValue;
        set { _progressValue = value; OnPropertyChanged(); }
    }
    public ICommand StartScanCommand { get; }
    public ICommand CancelScanCommand { get; }

    private SymbolViewModel _selectedSymbol;
    private string _selectedSymbolName;
    public string SelectedSymbolName
    {
        get => _selectedSymbolName;
        set
        {
            if (_selectedSymbolName != value)
            {
                // stop any previous symbol stream
                _engine.StopSymbol();

                _selectedSymbolName = value;
                OnPropertyChanged(nameof(SelectedSymbolName));

                if (!string.IsNullOrWhiteSpace(_selectedSymbolName))
                {
                    Console.WriteLine($"[Selection] charting {_selectedSymbolName}");
                    _chartManager.ClearAllSeries();

                    LiveStatusText = "Connecting...";
                    LiveStatusColor = "Orange";

                    // load history first
                    _ = LoadSymbolDataByNameAsync(_selectedSymbolName);

                    // then start streaming live updates
                    _engine.StartSymbol(_selectedSymbolName);

                    LiveStatusText = "Live";
                    LiveStatusColor = "LimeGreen";
                }
                else
                {
                    LiveStatusText = "Idle";
                    LiveStatusColor = "Gray";
                }
            }
        }
    }



    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool AllocConsole();
    #endregion

    #region Observable Collections
    public ObservableCollection<SymbolViewModel> Symbols { get; } = new ObservableCollection<SymbolViewModel>();
    public ObservableCollection<string> OverboughtSymbols => _scanner.OverboughtSymbols;
    public ObservableCollection<string> OversoldSynbols => _scanner.OversoldSymbols;
    #endregion

    #region Chart Properties
    public PlotModel PriceView => _chartManager.PriceView;
    public PlotModel RsiView => _chartManager.RsiView;
    public PlotModel VolumeView => _chartManager.VolumeView;
    #endregion

    #region Display Text Properties

    private string priceText;
    public string PriceText
    {
        get => priceText;
        set { priceText = value; OnPropertyChanged(nameof(PriceText)); }
    }

    private string rsiText;
    public string RsiText
    {
        get => rsiText;
        set { rsiText = value; OnPropertyChanged(nameof(RsiText)); }
    }

    private double rsiValue;
    public double RsiValue
    {
        get => rsiValue;
        set
        {
            rsiValue = value;
            OnPropertyChanged(nameof(RsiValue));
            RsiText = $"RSI: {RsiValue:F2}";
        }
    }

    private string volumeText;
    public string VolumeText
    {
        get => volumeText;
        set { volumeText = value; OnPropertyChanged(nameof(volumeText)); }
    }

    private string smaText = string.Empty;
    public string SmaText
    {
        get => smaText;
        set { smaText = value; OnPropertyChanged(nameof(SmaText)); }
    }
    private string _liveStatusText = "Idle";
    public string LiveStatusText
    {
        get => _liveStatusText;
        set { _liveStatusText = value; OnPropertyChanged(nameof(LiveStatusText)); }
    }

    private string _liveStatusColor = "Gray";
    public string LiveStatusColor
    {
        get => _liveStatusColor;
        set { _liveStatusColor = value; OnPropertyChanged(nameof(LiveStatusColor)); }
    }

    #endregion

    #region Selection
    public SymbolViewModel SelectedSymbol
    {
        get => _selectedSymbol;
        set
        {
            if(_selectedSymbol != value)
            {
                _selectedSymbol = value;
                OnPropertyChanged(nameof(SelectedSymbol));

                if(_selectedSymbol != null)
                {
                    Console.WriteLine($"[Selection] Selected {_selectedSymbol.Symbol}");

                    //Clear all chart data
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _chartManager.ClearAllSeries();
                        _priceSeries.Points.Clear();
                        _smaSeries.Points.Clear();
                        _upperSeries.Points.Clear();
                        _lowerSeries.Points.Clear();
                        _rsiSeries.Points.Clear();
                        _volumeSeries.Items.Clear();

                    });
                }
            }
        }
    }
    #endregion

    public MainViewModel()
    {
        AllocConsole();
        _chartManager = new ChartManager();
        _chartManager.PriceView.Series.Add(_priceSeries);
        _chartManager.PriceView.Series.Add(_smaSeries);
        _chartManager.PriceView.Series.Add(_upperSeries);
        _chartManager.PriceView.Series.Add(_lowerSeries);

        _chartManager.RsiView.Series.Add(_rsiSeries);
        _chartManager.VolumeView.Series.Add(_volumeSeries);

        /*
        var symbolList = new List<string> { "AAPL", "MSFT", "TSLA" };

        foreach (var s in symbolList)
            Symbols.Add(new SymbolViewModel(s));
        */
        var provider = new PolygonMarketDataProvider(apiKey);
        _provider = provider;
        _engine = new MarketDataEngine(_provider);
        _alertService = new AlertService();
        _emailService = new EmailService();
        _alertManager = new AlertManager(_alertService, _emailService);
        _scanner = new EquityScannerService(provider);

        StartScanCommand = new RelayCommand(async _ => await StartScanAsync(), _ => _cts == null);
        CancelScanCommand = new RelayCommand(_ => CancelScan(), _ => _cts != null);

        //Subscribe to events
        _engine.OnNewPrice += Engine_OnNewPrice;
        _engine.OnNewVolume += Engine_OnNewVolume;
        _engine.OnNewRSI += Engine_OnNewRsi;
        _engine.OnNewSMA += Engine_OnNewSma;
        _engine.Start();
        
    }

    #region Engine Event Handling

    private async Task StartScanAsync()
    {
        _cts = new CancellationTokenSource();

        var progress = new Progress<int>(value =>
        {
            ProgressValue = value;
        });
        try
        {
            await _scanner.ScanAllAsync(progress, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Scan canceled.");
        }
        finally
        {
            _cts = null;
        }
    }

    private void CancelScan()
    {
        _cts?.Cancel();
    }

    public async Task TestTickerFetchAsync()
    {
        var provider = new PolygonMarketDataProvider(apiKey);
        //var tickers = await provider.GetAllTickersAsync();
        var tickers = new List<string> { "AAPL", "MSFT", "TSLA", "NVDA", "AMZN" };
        Console.WriteLine($"Fetched {tickers.Count} tickers");
    }

    private async void Engine_OnEquityScanned(EquityScanResult result)
{
    Console.WriteLine($"[Chart Update] Received scan for {result.Symbol}");

    // --- Update the SymbolViewModel in the list ---
    var symbolVm = Symbols.FirstOrDefault(s => s.Symbol == result.Symbol);
    if (symbolVm != null)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            symbolVm.Price = result.Price;
            symbolVm.RSI = result.RSI;
            symbolVm.SMA = result.SMA;
            symbolVm.Volume = result.Volume;

            // Process alerts for this symbol
            foreach (var alert in _alertManager.Alerts)
            {
                if (alert.Symbol == result.Symbol)
                {
                    _alertManager.ProcessScanResult(result);
                }
            }
        });
    }

    // --- Skip chart update if this symbol isn't selected ---
    if (_selectedSymbol == null || result.Symbol != _selectedSymbol.Symbol)
    {
        Console.WriteLine($"[Chart Update] Skipped {result.Symbol} (not selected)");
        return;
    }

    // --- Get the latest timestamp from Polygon ---
        Application.Current.Dispatcher.Invoke(() =>
        {
            // Add new data point
            var timestamp = result.TimeStamp != default
                ? result.TimeStamp.ToLocalTime()
        :       DateTime.Now;
            double x = DateTimeAxis.ToDouble(timestamp);

            _priceSeries.Points.Add(new DataPoint(x, result.Price));
            _smaSeries.Points.Add(new DataPoint(x, result.SMA));
            _upperSeries.Points.Add(new DataPoint(x, result.Upper));
            _lowerSeries.Points.Add(new DataPoint(x, result.Lower));
            _rsiSeries.Points.Add(new DataPoint(x, result.RSI));
            _volumeSeries.Items.Add(new RectangleBarItem(x - 0.0005, 0, x + 0.0005, result.Volume));

            PriceView.InvalidatePlot(true);
            RsiView.InvalidatePlot(true);
            VolumeView.InvalidatePlot(true);

            
        });

    }


    #endregion

    #region Chart Updating

    private void UpdateCharts(EquityScanResult result)
    {
        double timestamp = DateTimeAxis.ToDouble(result.TimeStamp);


        _chartManager.UpdateRsiData(new List<DataPoint> { new DataPoint(timestamp, result.RSI) });
        _chartManager.UpdateVolumeData(new List<DataPoint> { new DataPoint(timestamp, result.Volume) });

        //Update labels
        PriceText = $"Price: {result.Price:F2}";
        SmaText = $"SMA: {result.Price:F2}";
        RsiText = $"RSI: {result.RSI:F2}";
        VolumeText = $"Volume: {result.Volume:F2}";
    }

    private void Engine_OnNewPrice(string symbol, double price)
    {
        if (symbol != _selectedSymbolName) return;

        Application.Current.Dispatcher.Invoke(async () =>
        {
            PriceText = $"Price: {price:F2}";
            _chartManager.AddPricePoint(price);

            // Flash animation for live indicator
            LiveStatusColor = "LightGreen";
            await Task.Delay(150);
            LiveStatusColor = "LimeGreen";
        });
    }


    private void Engine_OnNewVolume(string symbol, double volume)
    {
        if (symbol != _selectedSymbolName) return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            VolumeText = $"Volume: {volume:N0}";
            _chartManager.AddVolumePoint(volume);
        });
    }

    private void Engine_OnNewRsi(string symbol, double rsi)
    {
        if (symbol != _selectedSymbolName) return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            RsiText = $"RSI: {rsi:F2}";
            _chartManager.AddRsiPoint(rsi);
        });
    }

    private void Engine_OnNewSma(string symbol, double sma, double upper, double lower)
    {
        if (symbol != _selectedSymbolName) return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            SmaText = $"SMA: {sma:F2}";
            _chartManager.UpdateSmaBands(sma, upper, lower);
        });
    }

    private async Task LoadSymbolData(SymbolViewModel symbol)
    {
        if (symbol == null) return;

        // Clear previous chart data
        _chartManager.ClearAllSeries();

        await LoadHistoricalDataAsync(symbol.Symbol);

        _engine.OnEquityScanned += result =>
        {
            if(result.Symbol != symbol.Symbol) return;
            
            double timestamp = DateTimeAxis.ToDouble(DateTime.Now);

            _chartManager.UpdatePriceData(
                new List<DataPoint> { new DataPoint(timestamp, result.Price) },
                new List<DataPoint> { new DataPoint(timestamp, result.SMA) },
                new List<(DataPoint upper, DataPoint lower)>
                {
                    (new DataPoint(timestamp, result.Upper), new DataPoint(timestamp, result.Lower))
                }
             );

            _chartManager.UpdateRsiData(new List<DataPoint> { new DataPoint(timestamp, result.RSI) });
            _chartManager.UpdateVolumeData(new List<DataPoint> { new DataPoint(timestamp, result.Volume) });

            PriceText = $"Price: {result.Price:F2}";
            RsiText = $"RSI: {result.RSI:F2}";
            SmaText = $"SMA: {result.SMA:F2}";
            VolumeText = $"Volume: {result.Volume:F2}";
        };




        // Immediately push the latest known data for this symbol
        var lastPrice = _engine.GetLastPrice(symbol.Symbol);
        var lastSmaData = _engine.GetLastSma(symbol.Symbol);
        var lastRsi = _engine.GetLastRSI(symbol.Symbol);
        var lastVolume = _engine.GetLastVolume(symbol.Symbol);

        if (lastPrice.HasValue && lastSmaData.HasValue && lastRsi.HasValue && lastVolume.HasValue)
        {
            (double sma, double upper, double lower) = lastSmaData.Value;
            double timestamp = DateTimeAxis.ToDouble(DateTime.Now);

            _chartManager.UpdatePriceData(
                new List<DataPoint> { new DataPoint(timestamp, lastPrice.Value) },
                new List<DataPoint> { new DataPoint(timestamp, sma) },
                new List<(DataPoint upper, DataPoint lower)>
                {
            (new DataPoint(timestamp, upper), new DataPoint(timestamp, lower))
                }
            );

            _chartManager.UpdateRsiData(new List<DataPoint> { new DataPoint(timestamp, lastRsi.Value) });
            _chartManager.UpdateVolumeData(new List<DataPoint> { new DataPoint(timestamp, lastVolume.Value) });
        }


    }

    private async Task LoadSymbolDataByNameAsync(string symbol)
    {
        _chartManager.ClearAllSeries();

        var closes = await _provider.GetHistoricalClosesAsync(symbol, 50);
        var timestamps = await _provider.GetHistoricalTimestampsAsync(symbol, 50);
        if (closes.Count == 0) return;

        var pricePoints = closes.Zip(timestamps, (c, t) =>
            new OxyPlot.DataPoint(OxyPlot.Axes.DateTimeAxis.ToDouble(t), c)).ToList();

        _chartManager.UpdatePriceData(pricePoints, new List<OxyPlot.DataPoint>(), new List<(OxyPlot.DataPoint, OxyPlot.DataPoint)>());
    }

    private async Task LoadHistoricalDataAsync(string symbol)
    {
        // Clear any existing points
        _chartManager.ClearAllSeries();

        // --- Fetch historical data ---
        var closes = await _provider.GetHistoricalClosesAsync(symbol, 50);
        var timestamps = await _provider.GetHistoricalTimestampsAsync(symbol, 50);

        if (closes == null || timestamps == null || closes.Count == 0 || timestamps.Count == 0)
        {
            Console.WriteLine($"No data available for {symbol}");
            return;
        }

        var pricePoints = new List<DataPoint>();
        var smaPoints = new List<DataPoint>();
        var bollingerBands = new List<(DataPoint upper, DataPoint lower)>();
        var rsiPoints = new List<DataPoint>();

        int period = 14;

        // --- Build rolling indicators ---
        for (int i = 0; i < closes.Count && i < timestamps.Count; i++)
        {
            // Convert timestamp properly (UTC → Local)
            DateTime ts = timestamps[i].ToLocalTime();
            double x = DateTimeAxis.ToDouble(ts);
            double close = closes[i];

            pricePoints.Add(new DataPoint(x, close));

            if (i >= period)
            {
                // Rolling window for SMA & Bollinger
                var window = closes.Skip(i - period).Take(period).ToList();
                double localSma = window.Average();
                double sd = _engine.StdDev(window);
                double upper = localSma + 2 * sd;
                double lower = localSma - 2 * sd;

                smaPoints.Add(new DataPoint(x, localSma));
                bollingerBands.Add((new DataPoint(x, upper), new DataPoint(x, lower)));

                // Compute RSI for that window
                double rsi = _engine.CalculateRSI(window);
                rsiPoints.Add(new DataPoint(x, rsi));
            }
        }

        // --- Push data to chart manager ---
        _chartManager.UpdatePriceData(pricePoints, smaPoints, bollingerBands);
        _chartManager.UpdateRsiData(rsiPoints);

        Console.WriteLine($"[{symbol}] Historical data plotted: {closes.Count} points");
    }

    public void RefreshCharts(SymbolData data)
    {
        _chartManager.UpdatePriceData(data.PricePoints, data.SmaPoints, data.BollingerBands);
        _chartManager.UpdateRsiData(data.RsiPoints);
        _chartManager.UpdateVolumeData(data.VolumePoints);
    }
    #endregion

    #region Helper Methods

    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    #endregion
}
