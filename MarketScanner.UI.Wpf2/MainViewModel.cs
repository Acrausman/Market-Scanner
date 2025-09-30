using MarketScanner.UI;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

namespace MarketScanner.UI.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public PlotModel PriceView { get; }
        public PlotModel RsiView { get; }
        public PlotModel VolumeView { get; }

        private LineSeries priceSeries;
        private LineSeries smaSeries;
        private AreaSeries bollingerBands;
        private LineSeries rsiSeries;
        private RectangleBarSeries volumeSeries;

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

        private string volumeText;
        public string VolumeText
        {
            get => volumeText;
            set { volumeText = value; OnPropertyChanged(nameof(volumeText)); }
        }

        private string SmaText;
        public string smaText
        {
            get => smaText;
            set { smaText = value; OnPropertyChanged(nameof(smaText)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)      
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        

        private MarketDataEngine engine;
        private Dictionary<string, DateTime> lastTimestamps;
        private DateTime startTime;

        public MainViewModel()
        {
            startTime = DateTime.Now;
            lastTimestamps = new Dictionary<string, DateTime>();
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
            //Horizontal guide lines
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
            engine = new MarketDataEngine(new List<string> { "AAPL" }); // one symbol for now
            engine.OnNewPrice += Engine_OnNewPrice;
            engine.OnNewRSI += Engine_OnNewRSI;
            engine.OnNewSMA += Engine_OnNewSMA;
            engine.OnNewVolume += Engine_OnNewVolume;
            engine.OnTrigger += Engine_OnTrigger;

            engine.Start();
        }

        private void Engine_OnNewPrice(string symbol, double price)
        {
            DateTime now = DateTime.Now;
            double time = DateTimeAxis.ToDouble(now);

            App.Current.Dispatcher.Invoke(() =>
            {
                priceSeries.Points.Add(new DataPoint(time, price));
                PriceText = $"Price: {price:F2}";
                PriceView.InvalidatePlot(true);
            });
        }

        private void Engine_OnNewSMA(string symbol, double sma, double upper, double lower)
        {
            DateTime now = DateTime.Now;
            double time = DateTimeAxis.ToDouble(now);

            App.Current.Dispatcher.Invoke(() =>
            {
                smaSeries.Points.Add(new DataPoint(time, sma));

                //Bollinger Bands
                if(bollingerBands.Points.Count == bollingerBands.Points2.Count)
                {
                    bollingerBands.Points.Add(new DataPoint(time,lower));
                    bollingerBands.Points2.Insert(0, new DataPoint(time, lower));
                }
                else
                {
                    bollingerBands.Points.Add(new DataPoint(time, upper));
                    bollingerBands.Points2.Add(new DataPoint(time, lower));
                }

                
                PriceView.InvalidatePlot(true);
            });
        }

        private void Engine_OnNewRSI(string symbol, double rsi)
        {
            DateTime now = DateTime.Now;
            double time = DateTimeAxis.ToDouble(now);

            App.Current.Dispatcher.Invoke(() =>
            {
                rsiSeries.Points.Add(new DataPoint(time, rsi));
                
                if(rsi >= 70)
                {
                    var marker = new ScatterSeries
                    {
                        MarkerType = MarkerType.Triangle,
                        MarkerFill = OxyColors.Red,
                        MarkerSize = 5
                    };
                    marker.Points.Add(new ScatterPoint(time,rsi));
                    RsiView.Series.Add(marker);
                }
                else if(rsi <= 30)
                {
                    var marker = new ScatterSeries
                    {
                        MarkerType = MarkerType.Triangle,
                        MarkerFill = OxyColors.Green,
                        MarkerSize = 5

                    };
                    marker.Points.Add(new ScatterPoint(time,rsi));
                    RsiView.Series.Add(marker);
                }
                RsiView.InvalidatePlot(true);
                RsiText = $"RSI: {rsi:F2}";
            });
        }

        private void Engine_OnNewVolume(string symbol, double volume)
        {
            DateTime now = DateTime.Now;
            double time = DateTimeAxis.ToDouble(now);

            App.Current.Dispatcher.Invoke(() =>
            {
                double barWidth = 1.0 / 24 / 60; //about 1 minute wide
                volumeSeries.Items.Add(new RectangleBarItem(time - barWidth / 2, 0, time + barWidth / 2, volume));
                VolumeText = $"Volume: {volume:F2}";
                VolumeView.InvalidatePlot(true);
            });
        }

        private void Engine_OnTrigger(TriggerHit trigger)
        {
            Console.WriteLine($"Trigger: {trigger.Symbol} {trigger.TriggerName} at {trigger.Price}");
        }

    }



}
