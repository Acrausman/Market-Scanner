using MarketScanner.Data;
using MarketScanner.Data.Models;
using MarketScanner.Data.Providers;
using MarketScanner.Data.Services;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace MarketScanner.UI.Wpf.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        public PlotModel PriceView { get; }
        public PlotModel RsiView { get; }
        public PlotModel VolumeView { get; }

        private LineSeries priceSeries;
        private LineSeries smaSeries;
        private AreaSeries bollingerBands;
        private LineSeries rsiSeries;
        private RectangleBarSeries volumeSeries;

        private SymbolViewModel selectedSymbol;
        private string _currentChartSymbol; // Track currently displayed chart

        public SymbolViewModel SelectedSymbol
        {
            get => selectedSymbol;
            set
            {
                if (selectedSymbol != value)
                {
                    selectedSymbol = value;
                    OnPropertyChanged(nameof(SelectedSymbol));

                    _currentChartSymbol = selectedSymbol?.Symbol;
                    LoadSymbolData(selectedSymbol);
                }
            }
        }

        public ObservableCollection<SymbolViewModel> Symbols { get; }
            = new ObservableCollection<SymbolViewModel>();

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
            set { volumeText = value; OnPropertyChanged(nameof(VolumeText)); }
        }

        private string smaText = string.Empty;
        public string SmaText
        {
            get => smaText;
            set { smaText = value; OnPropertyChanged(nameof(SmaText)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private MarketDataEngine engine;
        private Dictionary<string, int> _cooldownCounters = new Dictionary<string, int>();
        private const int CooldownThreshold = 3;

        public MainViewModel()
        {
            AllocConsole(); // attach a debug console

            // ---------------------------
            // Price + SMA + Bollinger
            // ---------------------------

            PriceView = new PlotModel { Title = "Price" };
            PriceView.Legends.Add(new Legend
            {
                LegendPlacement = LegendPlacement.Outside,
                LegendPosition = LegendPosition.TopLeft,
                LegendOrientation = LegendOrientation.Horizontal
            });

            PriceView.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Price",
                IsPanEnabled = false,
                IsZoomEnabled = false
            });

            PriceView.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Time",
                StringFormat = "HH:mm:ss"
            });

            // Price line
            priceSeries = new LineSeries
            {
                Title = "Price",
                Color = OxyColors.SteelBlue,
                StrokeThickness = 2,
                LineStyle = LineStyle.Solid,
                // temporary test markers (remove after verifying)
                MarkerType = MarkerType.Circle,
                MarkerSize = 2,
                MarkerFill = OxyColors.SteelBlue
            };

            // SMA line
            smaSeries = new LineSeries
            {
                Title = "SMA14",
                Color = OxyColors.Red,
                StrokeThickness = 2,
                LineStyle = LineStyle.Solid
            };

            // Bollinger area (drawn first so lines come on top)
            bollingerBands = new AreaSeries
            {
                Title = "Bollinger Bands",
                // keep outline transparent to avoid covering lines
                Color = OxyColors.Transparent,
                // light translucent fill so lines are visible
                Fill = OxyColor.FromAColor(30, OxyColors.ForestGreen),
                StrokeThickness = 0
            };

            // Add in BACK → FRONT order (area first, then lines)
            PriceView.Series.Clear();
            PriceView.Series.Add(bollingerBands);
            PriceView.Series.Add(priceSeries);
            PriceView.Series.Add(smaSeries);

            PriceView.InvalidatePlot(true);


            // ---------------------------
            // RSI
            // ---------------------------
            RsiView = new PlotModel { Title = "RSI" };
            RsiView.Legends.Add(new Legend
            {
                LegendPlacement = LegendPlacement.Outside,
                LegendPosition = LegendPosition.TopRight,
                LegendOrientation = LegendOrientation.Horizontal
            });

            RsiView.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "RSI",
                Minimum = 0,
                Maximum = 100
            });
            RsiView.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Time",
                StringFormat = "HH:mm:ss"
            });

            rsiSeries = new LineSeries { 
                Title = "RSI", 
                Color = OxyColors.Orange 
            };

            var overboughtLine = new LineAnnotation
            {
                Type = LineAnnotationType.Horizontal,
                Y = 70,
                Color = OxyColors.Red,
                LineStyle = LineStyle.Dash,
                Text = "Overbought"
            };

            var oversoldLine = new LineAnnotation
            {
                Type = LineAnnotationType.Horizontal,
                Y = 30,
                Color = OxyColors.Green,
                LineStyle = LineStyle.Dash,
                Text = "Oversold"
            };

            RsiView.Annotations.Add(overboughtLine);
            RsiView.Annotations.Add(oversoldLine);
            RsiView.Series.Add(rsiSeries);

            // ---------------------------
            // Volume
            // ---------------------------
            VolumeView = new PlotModel { Title = "Volume" };
            VolumeView.Legends.Add(new Legend
            {
                LegendPlacement = LegendPlacement.Outside,
                LegendPosition = LegendPosition.TopRight,
                LegendOrientation = LegendOrientation.Horizontal
            });

            VolumeView.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Volume" });
            VolumeView.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Time",
                StringFormat = "HH:mm:ss"
            });

            volumeSeries = new RectangleBarSeries { Title = "Volume", FillColor = OxyColors.Gray };
            VolumeView.Series.Add(volumeSeries);

            // ---------------------------
            // Hook up MarketDataEngine
            // ---------------------------
            var symbols = new List<string> { "TSLA", "AAPL", "MSFT" };
            var provider = new PolygonMarketDataProvider("YISIR_KLqJAdX7U6ix6Pjkyx70C_QgpI");
            engine = new MarketDataEngine(symbols, provider);

            engine.OnNewPrice += Engine_OnNewPrice;
            engine.OnNewRSI += Engine_OnNewRSI;
            engine.OnNewSMA += Engine_OnNewSMA;
            engine.OnNewVolume += Engine_OnNewVolume;
            engine.OnTrigger += Engine_OnTrigger;
            engine.OnEquityScanned += Engine_OnEquityScanned;

            engine.Start();
        }

        // ---------------------------
        // Event handlers with chart symbol filtering
        // ---------------------------

        private void Engine_OnNewPrice(string symbol, double price)
        {
            if (_currentChartSymbol != symbol) return;

            DateTime now = DateTime.Now;

            SafeUI(() =>
            {
                // Diagnostics you can temporarily enable to inspect state
                // Console.WriteLine($"[DBG] PriceHandler: seriesPoints={priceSeries.Points.Count}");

                double lastTime = priceSeries.Points.Count > 0 ? priceSeries.Points.Last().X : double.MinValue;
                double time = DateTimeAxis.ToDouble(now);

                // ensure strictly increasing X
                if (time <= lastTime)
                    time = lastTime + TimeSpan.FromSeconds(1).TotalDays;

                // add new price point
                priceSeries.Points.Add(new DataPoint(time, price));
                PriceText = $"Price: {price:F2}";

                // add indicators if available
                if (engine.GetLastSma(symbol) is { Sma: var sma, Upper: var upper, Lower: var lower })
                {
                    smaSeries.Points.Add(new DataPoint(time, sma));
                    bollingerBands.Points.Add(new DataPoint(time, upper));
                    bollingerBands.Points2.Add(new DataPoint(time, lower));
                }

                // update Y-axis smoothly (keep margin)
                var leftAxis = PriceView.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left) as LinearAxis;
                if (leftAxis != null)
                {
                    double margin = (leftAxis.Maximum - leftAxis.Minimum) * 0.1;
                    if (price > leftAxis.Maximum - margin) leftAxis.Maximum = price + margin;
                    if (price < leftAxis.Minimum + margin) leftAxis.Minimum = Math.Max(0, price - margin);
                }

                // PAN the bottom (time) axis so newest point is visible (window size must match LoadSymbolData)
                var bottomAxis = PriceView.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom) as DateTimeAxis;
                if (bottomAxis != null)
                {
                    double window = TimeSpan.FromMinutes(15).TotalDays; // same window used in LoadSymbolData
                    bottomAxis.Maximum = time + TimeSpan.FromSeconds(1).TotalDays; // small padding
                    bottomAxis.Minimum = bottomAxis.Maximum - window;
                }

                // redraw quickly without recalculating everything
                Console.WriteLine($"[DBG] pricePoints={priceSeries.Points.Count} lastX={(priceSeries.Points.Count > 0 ? priceSeries.Points.Last().X : double.NaN)}");
                var bottom = PriceView.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom) as DateTimeAxis;
                Console.WriteLine($"[DBG] bottomMin={bottom?.Minimum} bottomMax={bottom?.Maximum}");
                Console.WriteLine($"[DBG] priceInSeriesAttachedToPlot={PriceView.Series.Contains(priceSeries)} seriesCount={PriceView.Series.Count}");

                PriceView.InvalidatePlot(false);
            });
        }





        private void Engine_OnNewRSI(string symbol, double rsi)
        {
            if (_currentChartSymbol != symbol) return;

            DateTime now = DateTime.Now;

            SafeUI(() =>
            {
                double lastTime = rsiSeries.Points.Count > 0 ? rsiSeries.Points.Last().X : double.MinValue;
                double time = DateTimeAxis.ToDouble(now);

                // Ensure strictly increasing X
                if (time <= lastTime)
                    time = lastTime + TimeSpan.FromSeconds(1).TotalDays;

                rsiSeries.Points.Add(new DataPoint(time, rsi));
                RsiValue = rsi;

                // Add oversold/overbought markers
                if (rsi >= 70 || rsi <= 30)
                {
                    var marker = new ScatterSeries
                    {
                        MarkerType = MarkerType.Triangle,
                        MarkerFill = rsi >= 70 ? OxyColors.Red : OxyColors.Green,
                        MarkerSize = 5
                    };
                    marker.Points.Add(new ScatterPoint(time, rsi));
                    RsiView.Series.Add(marker);
                }

                // Keep RSI axis fixed
                var rsiAxis = RsiView.Axes.First(a => a.Position == AxisPosition.Left);
                rsiAxis.Minimum = 0;
                rsiAxis.Maximum = 100;

                RsiView.InvalidatePlot(false); // fast refresh
            });
        }



        private void Engine_OnNewSMA(string symbol, double sma, double upper, double lower)
        {
            if (_currentChartSymbol != symbol) return;

            SafeUI(() =>
            {
                double time = DateTimeAxis.ToDouble(DateTime.Now);
                smaSeries.Points.Add(new DataPoint(time, sma));
                bollingerBands.Points.Add(new DataPoint(time, upper));
                bollingerBands.Points2.Add(new DataPoint(time, lower));
                SmaText = $"SMA: {sma:F2}";
                PriceView.InvalidatePlot(true);
            });
        }

        private void Engine_OnNewVolume(string symbol, double volume)
        {
            if (_currentChartSymbol != symbol) return;

            DateTime now = DateTime.Now;
            double lastTime = volumeSeries.Items.Count > 0
                ? volumeSeries.Items.Last().X1
                : double.MinValue;

            double startTime = DateTimeAxis.ToDouble(now - TimeSpan.FromMinutes(1));
            double endTime = DateTimeAxis.ToDouble(now);

            // Ensure strictly increasing
            if (startTime <= lastTime)
            {
                startTime = lastTime + TimeSpan.FromSeconds(1).TotalDays;
                endTime = startTime + TimeSpan.FromMinutes(1).TotalDays;
            }

            SafeUI(() =>
            {
                volumeSeries.Items.Add(new RectangleBarItem(startTime, 0, endTime, volume));
                VolumeText = $"Volume: {volume:F2}";

                // Smooth Y-axis
                var volumeAxis = VolumeView.Axes.First(a => a.Position == AxisPosition.Left);
                double margin = (volumeAxis.Maximum - volumeAxis.Minimum) * 0.1;

                if (volume > volumeAxis.Maximum - margin)
                    volumeAxis.Maximum = volume + margin;

                if (volume < volumeAxis.Minimum + margin)
                    volumeAxis.Minimum = Math.Max(0, volume - margin);

                VolumeView.InvalidatePlot(false); // fast refresh
            });
        }


        private void Engine_OnTrigger(TriggerHit trigger)
        {
            Console.WriteLine($"Trigger: {trigger.Symbol} {trigger.TriggerName} at {trigger.Price}");
        }

        private void Engine_OnEquityScanned(EquityScanResult result)
        {
            SafeUI(() =>
            {
                var symbolVm = Symbols.FirstOrDefault(s => s.Symbol == result.Symbol);
                bool isTriggered = true; // Always update for now

                if (isTriggered)
                {
                    _cooldownCounters[result.Symbol] = 0;

                    if (symbolVm == null)
                    {
                        symbolVm = new SymbolViewModel
                        {
                            Symbol = result.Symbol,
                            Price = result.Price,
                            RSI = result.RSI,
                            SMA = result.SMA,
                            Volume = result.Volume
                        };
                        Symbols.Add(symbolVm);
                    }
                    else
                    {
                        symbolVm.Price = result.Price;
                        symbolVm.RSI = result.RSI;
                        symbolVm.SMA = result.SMA;
                        symbolVm.Volume = result.Volume;
                    }
                }
                else
                {
                    if (symbolVm != null)
                    {
                        if (!_cooldownCounters.ContainsKey(result.Symbol))
                            _cooldownCounters[result.Symbol] = 0;

                        _cooldownCounters[result.Symbol]++;
                        if (_cooldownCounters[result.Symbol] >= CooldownThreshold)
                        {
                            Symbols.Remove(symbolVm);
                            _cooldownCounters.Remove(result.Symbol);
                        }
                    }
                }
            });
        }

        private async void LoadSymbolData(SymbolViewModel symbolVm)
        {
            if (symbolVm == null) return;

            // mark active immediately
            _currentChartSymbol = symbolVm.Symbol;
            Console.WriteLine($"Loading charts for {_currentChartSymbol}...");

            // clear existing points but keep same series objects
            SafeUI(() =>
            {
                priceSeries.Points.Clear();
                smaSeries.Points.Clear();
                bollingerBands.Points.Clear();
                bollingerBands.Points2.Clear();
                rsiSeries.Points.Clear();
                volumeSeries.Items.Clear();

                // ensure series order: area first, then lines
                if (PriceView.Series.Contains(bollingerBands)) PriceView.Series.Remove(bollingerBands);
                if (PriceView.Series.Contains(priceSeries)) PriceView.Series.Remove(priceSeries);
                if (PriceView.Series.Contains(smaSeries)) PriceView.Series.Remove(smaSeries);
                PriceView.Series.Add(bollingerBands);
                PriceView.Series.Add(priceSeries);
                PriceView.Series.Add(smaSeries);

                // make area very light so lines remain visible
                bollingerBands.Fill = OxyColor.FromAColor(30, OxyColors.ForestGreen);
                bollingerBands.Color = OxyColors.Transparent;

                // ensure price uses lines (no markers)
                priceSeries.MarkerType = MarkerType.None;

                PriceView.InvalidatePlot(false);
            });

            try
            {
                var closes = await engine.Provider.GetHistoricalClosesAsync(symbolVm.Symbol, 50);
                var timestamps = await engine.Provider.GetHistoricalTimestampsAsync(symbolVm.Symbol, 50);
                (double latestPrice, double latestVolume) = await engine.Provider.GetQuoteAsync(symbolVm.Symbol);

                if (closes == null || closes.Count == 0)
                {
                    Console.WriteLine($"No historical closes for {symbolVm.Symbol}");
                    return;
                }

                // --- Build a time array that lines up with live ticks ---
                // If provider timestamps look like DAILY (midnight times spanning many days),
                // map them to a recent minute-spaced range so historical + live are continuous.
                DateTime now = DateTime.Now;
                List<DateTime> times;
                bool useSynthetic = false;

                if (timestamps == null || timestamps.Count != closes.Count)
                {
                    useSynthetic = true;
                }
                else
                {
                    // if span is larger than reasonable intraday span -> treat as daily
                    var span = timestamps.Last() - timestamps.First();
                    if (span.TotalDays >= Math.Max(1, closes.Count / 2.0)) // heuristic: many days -> daily
                        useSynthetic = true;
                }

                if (useSynthetic)
                {
                    // evenly spaced 1-minute points that end at "now"
                    times = Enumerable.Range(0, closes.Count)
                                      .Select(i => now.AddMinutes(i - closes.Count + 1))
                                      .ToList();
                }
                else
                {
                    // use provider timestamps (ascending)
                    times = timestamps.OrderBy(t => t).ToList();
                }

                // --- Compute SMA and Bollinger locally to be deterministic ---
                int period = 14;
                var smaList = new List<double>(closes.Count);
                var upperBand = new List<double>(closes.Count);
                var lowerBand = new List<double>(closes.Count);

                for (int i = 0; i < closes.Count; i++)
                {
                    if (i + 1 >= period)
                    {
                        var window = closes.Skip(i + 1 - period).Take(period).ToList();
                        double sma = window.Average();
                        double mean = sma;
                        double variance = window.Sum(x => (x - mean) * (x - mean)) / period; // population variance
                        double sd = Math.Sqrt(variance);

                        smaList.Add(sma);
                        upperBand.Add(sma + 2 * sd);
                        lowerBand.Add(sma - 2 * sd);
                    }
                    else
                    {
                        smaList.Add(double.NaN);
                        upperBand.Add(double.NaN);
                        lowerBand.Add(double.NaN);
                    }
                }

                // --- Add points to the series on the UI thread ---
                SafeUI(() =>
                {
                    double barWidth = TimeSpan.FromMinutes(1).TotalDays;

                    for (int i = 0; i < closes.Count; i++)
                    {
                        double time = DateTimeAxis.ToDouble(times[i]);

                        // price
                        if (!double.IsNaN(closes[i]))
                            priceSeries.Points.Add(new DataPoint(time, closes[i]));

                        // sma & bands (only when available)
                        if (!double.IsNaN(smaList[i]))
                        {
                            smaSeries.Points.Add(new DataPoint(time, smaList[i]));
                            bollingerBands.Points.Add(new DataPoint(time, upperBand[i]));
                            bollingerBands.Points2.Add(new DataPoint(time, lowerBand[i]));
                        }

                        // placeholder volume bar (height 0 for now)
                        volumeSeries.Items.Add(new RectangleBarItem(time - barWidth / 2, 0, time + barWidth / 2, 0));
                    }

                    // set latest volume into last bar
                    if (volumeSeries.Items.Count > 0)
                    {
                        var lastBar = volumeSeries.Items.Last();
                        lastBar.Y1 = latestVolume;
                    }

                    // add last RSI point at 'now'
                    rsiSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(DateTime.Now), engine.CalculateRSI(closes)));

                    // autoscale axes to data
                    PriceView.ResetAllAxes();

                    // set a time window that ends at 'now' so live ticks and historical points share view
                    var bottomAxis = PriceView.Axes.FirstOrDefault(a => a.Position == AxisPosition.Bottom) as DateTimeAxis;
                    if (bottomAxis != null)
                    {
                        double maxTime = DateTimeAxis.ToDouble(times.Last());
                        // If synthetic used, times.Last() is near 'now'; if provider daily used, times.Last() was retained
                        bottomAxis.Maximum = DateTimeAxis.ToDouble(DateTime.Now) + TimeSpan.FromSeconds(1).TotalDays;
                        double window = TimeSpan.FromMinutes(60).TotalDays; // show 60 minutes by default
                        bottomAxis.Minimum = bottomAxis.Maximum - window;
                    }

                    // final draw
                    PriceView.InvalidatePlot(true);
                    RsiView.InvalidatePlot(true);
                    VolumeView.InvalidatePlot(true);

                    // debug
                    Console.WriteLine($"[CHK] priceSeries={priceSeries.Points.Count} smaSeries={smaSeries.Points.Count} bollinger={bollingerBands.Points.Count} lower={bollingerBands.Points2.Count}");
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Chart Load Error] {ex.Message}");
            }
        }





        private void SafeUI(Action action)
        {
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
                action();
            else
                System.Windows.Application.Current.Dispatcher.BeginInvoke(action);
        }
    }
}
