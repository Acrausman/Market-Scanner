using MarketScanner.Data;
using MarketScanner.Data.Services;
using MarketScanner.Data.Providers;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using MarketScanner.Data.Models;
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

        public ObservableCollection<SymbolViewModel> Symbols { get; }
            = new ObservableCollection<SymbolViewModel>();
        private string priceText;
        public string PriceText
        {
            get => priceText;
            set { priceText = value; OnPropertyChanged(nameof(priceText)); }
        }

        private string rsiText;
        public string RsiText
        {
            get => rsiText;
            set { rsiText = value; OnPropertyChanged(nameof (rsiText)); } 
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

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)      
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        

        private MarketDataEngine engine;
        private Dictionary<string, DateTime> lastTimestamps;
        private DateTime startTime;

        private Dictionary<string, int> _cooldownCounters = new Dictionary<string, int>();
        private const int CooldownThreshold = 3;

        public MainViewModel()
        {
            AllocConsole(); // attach a debug console

            startTime = DateTime.Now;
            lastTimestamps = new Dictionary<string, DateTime>();

            //_ = TestYahooProvider();
            // ---------------------------
            // Price + SMA + Bollinger
            // ---------------------------
            PriceView = new PlotModel { Title = "Price" };
            PriceView.Legends.Add(new Legend
            {
                LegendPlacement = LegendPlacement.Outside,
                LegendPosition = LegendPosition.TopRight,
                LegendOrientation = LegendOrientation.Vertical
            });

            PriceView.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Price" });
            PriceView.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Time",
                StringFormat = "HH:mm:ss"
            });

            priceSeries = new LineSeries { Title = "Price", Color = OxyColors.SteelBlue };
            smaSeries = new LineSeries { Title = "SMA14", Color = OxyColors.Red, StrokeThickness = 2 };
            bollingerBands = new AreaSeries
            {
                Title = "Bollinger Bands",
                Color = OxyColors.Transparent,
                Fill = OxyColor.FromAColor(60, OxyColors.Green)
            };

            PriceView.Series.Add(priceSeries);
            PriceView.Series.Add(smaSeries);
            PriceView.Series.Add(bollingerBands);

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

            rsiSeries = new LineSeries { Title = "RSI", Color = OxyColors.Orange };

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
            var symbols = new List<string> { "AAPL", "MSFT", "TSLA" };
            var provider = new PolygonMarketDataProvider("YISIR_KLqJAdX7U6ix6Pjkyx70C_QgpI\t");
            engine = new MarketDataEngine(symbols, provider);   // ✅ no shadowing
            engine.OnNewPrice += Engine_OnNewPrice;
            engine.OnNewRSI += Engine_OnNewRSI;
            engine.OnNewSMA += Engine_OnNewSMA;
            engine.OnNewVolume += Engine_OnNewVolume;
            engine.OnTrigger += Engine_OnTrigger;
            engine.OnEquityScanned += Engine_OnEquityScanned;
            engine.Start();
        }

        private void Engine_OnNewPrice(string symbol, double price)
        {
            DateTime now = DateTime.Now;
            double time = DateTimeAxis.ToDouble(now);

            SafeUI(() =>
            {
                priceSeries.Points.Add(new DataPoint(time, price));
                PriceText = $"Price: {price:F2}";
                PriceView.InvalidatePlot(true);
            });
        }

        private void Engine_OnNewSMA(string symbol, double sma, double upper, double lower)
        {
            SmaText = $"SMA: {sma:F2}";
            DateTime now = DateTime.Now;
            double time = DateTimeAxis.ToDouble(now);

            SafeUI(() =>
            {
                smaSeries.Points.Add(new DataPoint(time, sma));

                bollingerBands.Points.Add(new DataPoint(time, upper));
                bollingerBands.Points2.Insert(0, new DataPoint(time, lower));

                PriceView.InvalidatePlot(true);
            });
        }

        private void Engine_OnNewRSI(string symbol, double rsi)
        {
            DateTime now = DateTime.Now;
            double time = DateTimeAxis.ToDouble(now);

            SafeUI(() =>
            {
                rsiSeries.Points.Add(new DataPoint(time, rsi));

                // markers for oversold/overbought
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

                RsiValue = rsi;
                RsiView.InvalidatePlot(true);
            });
        }

        private void Engine_OnNewVolume(string symbol, double volume)
        {
            DateTime now = DateTime.Now;
            double time = DateTimeAxis.ToDouble(now);

            SafeUI(() =>
            {
                double barWidth = 1.0 / 24 / 60; // about 1 minute wide
                volumeSeries.Items.Add(new RectangleBarItem(time - barWidth / 2, 0, time + barWidth / 2, volume));
                VolumeText = $"Volume: {volume:F2}";
                VolumeView.InvalidatePlot(true);
            });
        }

        private void Engine_OnTrigger(TriggerHit trigger)
        {
            Console.WriteLine($"Trigger: {trigger.Symbol} {trigger.TriggerName} at {trigger.Price}");
        }

        private void Engine_OnEquityScanned(EquityScanResult result)
        {
            Console.WriteLine($"Equity scanned: {result.Symbol}: {result.Price}");
            SafeUI(() =>
            {
                // ✅ Search existing VM
                var symbolVm = Symbols.FirstOrDefault(s => s.Symbol == result.Symbol);
                bool isTriggered = result.RSI >= 70 || result.RSI <= 30;

                if (isTriggered)
                {
                    //Reset cooldown
                    _cooldownCounters[result.Symbol] = 0;

                    if (symbolVm == null)
                    {
                        //Add new
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
                        //Update existing
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
                        //Increment cooldown
                        if (!_cooldownCounters.ContainsKey(result.Symbol))
                            _cooldownCounters[result.Symbol] = 0;

                        _cooldownCounters[result.Symbol]++;

                        //Remove only if cooldown exceeded
                        if (_cooldownCounters[result.Symbol] >= CooldownThreshold)
                        {
                            Symbols.Remove(symbolVm);
                            _cooldownCounters.Remove(result.Symbol);
                        }
                    }
                }
                // ✅ These property sets now update UI automatically

            });
        }


        public async Task TestPolygonProvider()
        {
            var provider = new PolygonMarketDataProvider("YISIR_KLqJAdX7U6ix6Pjkyx70C_QgpI");
            try
            {
                var quote = await provider.GetQuoteAsync("AAPL");
                Console.WriteLine($"Price: {quote.price}, Volume: {quote.volume}");
            }
            catch (Flurl.Http.FlurlHttpException fex)
            {
                int statusCode = fex.Call.Response?.StatusCode ?? 0;
                string body = fex.Call.Response != null ? await fex.GetResponseStringAsync() : "<no body>";
                Console.WriteLine($"[FlurlHttpException] Status code: {statusCode}");
                Console.WriteLine($"Response body: {body}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Unexpected Exception] {ex.Message}");
            }
        }

        private void SafeUI(Action action)
        {
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
                action();
            else
                System.Windows.Application.Current.Dispatcher.Invoke(action);
        }


    }


}
