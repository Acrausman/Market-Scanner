using MarketScanner.Data;
using MarketScanner.Data.Models;
using MarketScanner.UI.Wpf.Services;
using MarketScanner.UI.Wpf.ViewModels;
using OxyPlot;
using OxyPlot.Axes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

public class MainViewModel : INotifyPropertyChanged
{
    #region Fields
    private readonly ChartManager _chartManager;
    private readonly MarketDataEngine _engine;
    private string apiKey = "YISIR_KLqJAdX7U6ix6Pjkyx70C_QgpI";

    private List<DataPoint> _pricePoints = new();
    private List<DataPoint> _smaPoints = new();
    private List<(DataPoint upper, DataPoint lower)> _bollingerPoints = new();
    private List<DataPoint> _rsiPoints = new();
    private List<DataPoint> _volumePoints = new();

    private SymbolViewModel _selectedSymbol;

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool AllocConsole();
    #endregion

    #region Observable Collections
    public ObservableCollection<SymbolViewModel> Symbols { get; } = new ObservableCollection<SymbolViewModel>();
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
    #endregion

    #region Selection
    public SymbolViewModel SelectedSymbol
    {
        get => _selectedSymbol;
        set
        {
            _selectedSymbol = value;
            OnPropertyChanged(nameof(SelectedSymbol));
            if (_selectedSymbol != null)
                Console.WriteLine($"[Selection] Selected {_selectedSymbol.Symbol}");
        }
    }
    #endregion

    public MainViewModel()
    {
        AllocConsole();
        _chartManager = new ChartManager();

        var symbolList = new List<string> { "AAPL", "MSFT", "TSLA" };

        foreach (var s in symbolList)
            Symbols.Add(new SymbolViewModel(s));

        var provider = new PolygonMarketDataProvider(apiKey);
        _engine = new MarketDataEngine(symbolList, provider);

        //Subscribe to events
        _engine.OnEquityScanned += Engine_OnEquityScanned;
        _engine.EnableDebugLogging = true;
        _engine.Start();
    }

    #region Engine Event Handling
    private void Engine_OnEquityScanned(EquityScanResult result)
    {
        Console.WriteLine($"[Chart Update] Recieved scan for {result.Symbol}");

        var symbolVm = Symbols.FirstOrDefault(s => s.Symbol == result.Symbol);
        if (symbolVm != null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                symbolVm.Price = result.Price;
                symbolVm.RSI = result.RSI;
                symbolVm.SMA = result.SMA;
                symbolVm.Volume = result.Volume;
            });
        }

        if (_selectedSymbol == null || result.Symbol != _selectedSymbol.Symbol)
        {
            Console.WriteLine($"[Chart Update] Skipped {result.Symbol} (not selected)");
            return;
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
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

        });

        
        /*double timestamp = DateTimeAxis.ToDouble(DateTime.Now);
        
        _pricePoints.Add(new DataPoint(timestamp, result.Price));
        _smaPoints.Add(new DataPoint(timestamp, result.Price));
        _bollingerPoints.Add((new DataPoint(timestamp, result.Upper), new DataPoint(timestamp + 1, result.Lower)));
        _rsiPoints.Add(new DataPoint(timestamp, result.RSI));
        _volumePoints.Add(new DataPoint(timestamp, result.Volume));
        _chartManager.UpdatePriceData(_pricePoints, _smaPoints, _bollingerPoints);
        _chartManager.UpdateRsiData(_rsiPoints);
        _chartManager.UpdateVolumeData(_volumePoints);*/
    }

    #endregion

    /*private void HandlePriceUpdate(double price, double? sma = null, double? upper = null, double? lower = null)
    {
        double timestamp = DateTimeAxis.ToDouble(DateTime.Now);

        _pricePoints.Add(new DataPoint(timestamp, price));

        if(sma.HasValue)
            _smaPoints.Add(new DataPoint(timestamp, sma.Value));
        if (upper.HasValue && lower.HasValue)
            _bollingerPoints.Add((new DataPoint(timestamp, upper.Value), new DataPoint(timestamp, lower.Value)));
        _chartManager.UpdatePriceData(_pricePoints, _smaPoints, _bollingerPoints);

        PriceText = $"Price: {price:F2}";
        if (sma.HasValue) SmaText = $"SMA: {sma:F2}";
    }

    private void HandleRsiUpdate(double rsi)
    {
        double timestamp = DateTimeAxis.ToDouble(DateTime.Now);
        _rsiPoints.Add(new DataPoint(timestamp, rsi));
        _chartManager.UpdateRsiData(_rsiPoints);
        RsiText = $"RSI: {rsi:F2}";
    }

    private void HandleVolumeUpdate(double volume)
    {
        double timestamp = DateTimeAxis.ToDouble(DateTime.Now);
        _volumePoints.Add(new DataPoint(timestamp, volume));
        _chartManager.UpdateVolumeData(_volumePoints);
        VolumeText = $"Volume: {volume:F2}";
    }*/

    #region Chart Updating

    private void UpdateCharts(EquityScanResult result)
    {
        double timestamp = DateTimeAxis.ToDouble(DateTime.Now);

        _chartManager.UpdatePriceData(
            new List<DataPoint> { new DataPoint(timestamp, result.Price) },
            new List<DataPoint> { new DataPoint(timestamp, result.SMA) },
            new List<(DataPoint upper, DataPoint lower)>
            {
                (new DataPoint(timestamp, result.Upper), new DataPoint(timestamp, result.Lower))
            });

        _chartManager.UpdateRsiData(new List<DataPoint> { new DataPoint(timestamp, result.RSI) });
        _chartManager.UpdateVolumeData(new List<DataPoint> { new DataPoint(timestamp, result.Volume) });

        //Update labels
        PriceText = $"Price: {result.Price:F2}";
        SmaText = $"SMA: {result.Price:F2}";
        RsiText = $"RSI: {result.RSI:F2}";
        VolumeText = $"Volume: {result.Volume:F2}";
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
        var lastSma = _engine.GetLastSma(symbol.Symbol);
        var lastRsi = _engine.GetLastRSI(symbol.Symbol);
        var lastVolume = _engine.GetLastVolume(symbol.Symbol);

        if (lastPrice.HasValue && lastSma.HasValue && lastRsi.HasValue && lastVolume.HasValue)
        {
            double timestamp = DateTimeAxis.ToDouble(DateTime.Now);

            _chartManager.UpdatePriceData(
                new List<DataPoint> { new DataPoint(timestamp, lastPrice.Value) },
                new List<DataPoint> { new DataPoint(timestamp, lastSma.Value.Sma) },
                new List<(DataPoint upper, DataPoint lower)>
                {
                (new DataPoint(timestamp, lastSma.Value.Upper), new DataPoint(timestamp, lastSma.Value.Lower))
                }
            );

            _chartManager.UpdateRsiData(new List<DataPoint> { new DataPoint(timestamp, lastRsi.Value) });
            _chartManager.UpdateVolumeData(new List<DataPoint> { new DataPoint(timestamp, lastVolume.Value) });
        }
    }

    private async Task LoadHistoricalDataAsync(string symbol)
    {
        var data = _engine.GetHistoricalData(symbol);
        if (data == null || data.Count == 0) return;

        var pricePoints = new List<DataPoint>();
        var smaPoints = new List<DataPoint>();
        var bollingerbands = new List<(DataPoint upper, DataPoint lower)>();
        var rsiPoints = new List<DataPoint>();
        var volumePoints = new List<DataPoint>();

        foreach ( var d in data)
        {
            double x = DateTimeAxis.ToDouble(d.Timestamp);
            pricePoints.Add(new DataPoint(x, d.Price));
            smaPoints.Add(new DataPoint(x, d.SMA));
            bollingerbands.Add((new DataPoint(x, d.UpperBand), new DataPoint(x, d.LowerBand)));
            rsiPoints.Add(new DataPoint(x, d.RSI));
            volumePoints.Add(new DataPoint(x, d.Volume));
        }

        _chartManager.UpdatePriceData(pricePoints, smaPoints, bollingerbands);
        _chartManager.UpdateRsiData(rsiPoints);
        _chartManager.UpdateVolumeData(volumePoints);
    }

    public void RefreshCharts(SymbolData data)
    {
        _chartManager.UpdatePriceData(data.PricePoints, data.SmaPoints, data.BollingerBands);
        _chartManager.UpdateRsiData(data.RsiPoints);
        _chartManager.UpdateVolumeData(data.VolumePoints);
    }
    #endregion

    #region INotifyPropertyChanged
    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    #endregion
}
