using MarketScanner.Data;
using MarketScanner.Data.Models;
using MarketScanner.Data.Providers;
using MarketScanner.Data.Services;
using MarketScanner.Data.Services.Indicators;
using MarketScanner.UI.Wpf.Services;
using OxyPlot;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace MarketScanner.UI.Wpf.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly PolygonMarketDataProvider _provider;
        private readonly MarketDataEngine _engine;

        public ChartViewModel Chart { get; }
        public ScannerViewModel Scanner { get; }

        private string _consoleText;
        public string ConsoleText
        {
            get => _consoleText;
            set { _consoleText = value; OnPropertyChanged(); }
        }

        public void Log(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ConsoleText += $"[{DateTime.Now:HH:mm:ss}]";
            });
        }

        public ICommand StartScanCommand { get; }
        public ICommand StopScanCommand { get; }

        private readonly Dictionary<string, EquityScanResult> _latestData = new();

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        private string _selectedSymbol;
        public string SelectedSymbol
        {
            get => _selectedSymbol;
            set
            {
                if (_selectedSymbol != value)
                {
                    _selectedSymbol = value;
                    OnPropertyChanged();

                    if (!string.IsNullOrWhiteSpace(_selectedSymbol))
                        _ = LoadSymbolAsync(_selectedSymbol);
                }
            }
        }

        private string _statusText = "Idle";
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public MainViewModel()
        {
            AllocConsole();
            Log("=== Market Scanner Console Initialized ===");


            string apiKey = "YISIR_KLqJAdX7U6ix6Pjkyx70C_QgpI";

            _provider = new PolygonMarketDataProvider(apiKey);
            _engine = new MarketDataEngine(_provider);
            _provider.VerifyPolygonBarAlignmentAsync("AAMI", 10);
            _provider.VerifyPolygonBarAlignmentAsync("TSLA", 10);

            Chart = new ChartViewModel();
            Scanner = new ScannerViewModel(new EquityScannerService(_provider));

            StartScanCommand = new RelayCommand(async _ => await StartScanAsync(), _ => true);
            StopScanCommand = new RelayCommand(_ => Scanner.Stop(), _ => true);

            // 🔹 Event Subscriptions (Buffered updates)
            _engine.OnNewPrice += (sym, price) => UpdateCache(sym, data => data.Price = price);
            _engine.OnNewRSI += (sym, rsi) => UpdateCache(sym, data => data.RSI = rsi);
            _engine.OnNewSMA += (sym, sma, upper, lower) => UpdateCache(sym, data =>
            {
                data.SMA = sma;
                data.Upper = upper;
                data.Lower = lower;
            });
            _engine.OnNewVolume += (sym, vol) => UpdateCache(sym, data => data.Volume = vol);

            _engine.OnEquityScanned += result =>
            {
                if (result.Symbol == SelectedSymbol)
                    Chart.Update(result);
            };

            var bars = _provider.GetHistoricalBarsAsync("AAPL");

        }

        private async Task StartScanAsync()
        {
            StatusText = "Scanning...";
            var progress = new Progress<int>(v => StatusText = $"Progress: {v}%");
            await Scanner.StartAsync(progress);
            StatusText = "Scan complete";
        }

        // 🟢 Load Historical + Start Live
        private async Task LoadSymbolAsync(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return;

            Log($"\n[Selection] User selected {symbol}");
            StatusText = $"Loading {symbol} data...";
            Chart.Clear();

            try
            {
                // 1️⃣ Historical context
                await Chart.LoadHistoricalAsync(symbol, _provider);

                // 2️⃣ Start real-time updates
                _engine.StopSymbol();
                _engine.StartSymbol(symbol);

                // 3️⃣ Fetch a fresh quote
                var (price, volume) = await _provider.GetQuoteAsync(symbol);
                var closes = await _provider.GetHistoricalClosesAsync(symbol, 120);
                double sma = closes.Count >= 14 ? closes[^14..].Average() : closes.Average();
                double sd = _engine.StdDev(closes);
                double upper = sma + 2 * sd;
                double lower = sma - 2 * sd;
                double rsi = RsiCalculator.Calculate(closes);

                var result = new EquityScanResult
                {
                    Symbol = symbol,
                    Price = price,
                    RSI = rsi,
                    SMA = sma,
                    Upper = upper,
                    Lower = lower,
                    Volume = volume,
                    TimeStamp = DateTime.Now
                };

                Chart.Update(result);
                StatusText = $"Streaming {symbol} (RSI={rsi:F1})";
                Log($"[LoadSymbol] Ready: {symbol} RSI={rsi:F1}, SMA={sma:F2}, Price={price:F2}");
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading {symbol}";
                Log($"[LoadSymbol] ❌ {symbol}: {ex.Message}");
            }
        }

        // 🧩 Cache + merge updates before sending to chart
        private void UpdateCache(string symbol, Action<EquityScanResult> updateAction)
        {
            if (symbol != SelectedSymbol) return;

            if (!_latestData.ContainsKey(symbol))
                _latestData[symbol] = new EquityScanResult { Symbol = symbol };

            var data = _latestData[symbol];
            updateAction(data);
            data.TimeStamp = DateTime.Now;

            TryUpdateChart(symbol);
        }

        public async Task TestRsiAsync()
        {
            var bars = await _provider.GetHistoricalBarsAsync("CFSB", 120, adjusted: false);
            var closes = bars.Select(b => b.Close).ToList();

            Console.WriteLine($"[Debug] Using {closes.Count} closes; latest={closes.Last():F2}, avgΔ={(closes.Last() - closes.First()) / closes.First() * 100:F2}%");
            Console.WriteLine("RSI(14) = " + RsiCalculator.Calculate(closes).ToString("F2"));
        }


        private void TryUpdateChart(string symbol)
        {
            var data = _latestData[symbol];

            if (data.Price <= 0 || double.IsNaN(data.RSI) || double.IsNaN(data.SMA))
                return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                Chart.Update(data);
                Log($"[Chart] Updating {symbol}: Price={data.Price:F2}, RSI={data.RSI:F2}");
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
