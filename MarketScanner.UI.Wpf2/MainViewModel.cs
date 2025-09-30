using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using System.Collections.Generic;
using MarketScanner.UI;
using System;

namespace MarketScanner.UI.ViewModels
{
    public class MainViewModel
    {
        public PlotModel PriceView { get; }
        public PlotModel RsiView { get; }
        public PlotModel VolumeView { get; }

        private LineSeries priceSeries;
        private LineSeries smaSeries;
        private AreaSeries bollingerBands;
        private LineSeries rsiSeries;
        private RectangleBarSeries volumeSeries;

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
                RsiView.InvalidatePlot(true);
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
                VolumeView.InvalidatePlot(true);
            });
        }

        private void Engine_OnTrigger(TriggerHit trigger)
        {
            Console.WriteLine($"Trigger: {trigger.Symbol} {trigger.TriggerName} at {trigger.Price}");
        }

    }



}
