using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace MarketScanner.UI.Wpf
{
    public partial class MainWindow : Window
    {
        private PlotModel MyPlotModel;

        private LineSeries priceSeries;
        private LineSeries smaSeries;
        private AreaSeries bollingerArea;
        private RectangleBarSeries volumeSeries;
        private LineSeries rsiSeries;

        private List<DataPoint> pricePoints = new();
        private List<double> volumePoints = new();
        private Random rng = new();

        private DispatcherTimer timer;

        private int smaPeriod = 14;
        private double bollingerMultiplier = 2.0;
        private int maxPoints = 50;

        public MainWindow()
        {
            InitializeComponent();
            SetupChart();
            GenerateInitialData();
            StartLiveDummyData();
            DataContext = this;
        }

        private void SetupChart()
        {
            MyPlotModel = new PlotModel { Title = "Market Scanner Demo" };

            // DateTime X-axis
            var dateAxis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "HH:mm:ss",
                Title = "Time",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                IntervalType = DateTimeIntervalType.Seconds,
                MinorIntervalType = DateTimeIntervalType.Seconds
            };
            MyPlotModel.Axes.Add(dateAxis);

            // Price axis (left)
            var priceAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Key = "PriceAxis",
                Title = "Price",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot
            };
            MyPlotModel.Axes.Add(priceAxis);

            // RSI axis (right)
            var rsiAxis = new LinearAxis
            {
                Position = AxisPosition.Right,
                Key = "RSIAxis",
                Title = "RSI",
                Minimum = 0,
                Maximum = 100,
                StartPosition = 0.25,
                EndPosition = 0.5,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot
            };
            MyPlotModel.Axes.Add(rsiAxis);

            // Volume axis (bottom)
            var volumeAxis = new LinearAxis
            {
                Position = AxisPosition.Right,
                Key = "VolumeAxis",
                Minimum = 0,
                StartPosition = 0,
                EndPosition = 0.2,
                Title = "Volume"
            };
            MyPlotModel.Axes.Add(volumeAxis);

            // Price series
            priceSeries = new LineSeries { Title = "Price", YAxisKey = "PriceAxis", Color = OxyColors.SteelBlue };
            MyPlotModel.Series.Add(priceSeries);

            // SMA series
            smaSeries = new LineSeries { Title = "SMA", YAxisKey = "PriceAxis", Color = OxyColors.Red, StrokeThickness = 2 };
            MyPlotModel.Series.Add(smaSeries);

            // Bollinger bands
            bollingerArea = new AreaSeries
            {
                Title = "Bollinger Bands",
                YAxisKey = "PriceAxis",
                Color = OxyColors.Transparent,
                Fill = OxyColor.FromAColor(60, OxyColors.Green),
                StrokeThickness = 1,
                LineStyle = LineStyle.Dash
            };
            MyPlotModel.Series.Add(bollingerArea);

            // Volume bars
            volumeSeries = new RectangleBarSeries { Title = "Volume", YAxisKey = "VolumeAxis", FillColor = OxyColors.Gray };
            MyPlotModel.Series.Add(volumeSeries);

            // RSI line
            rsiSeries = new LineSeries { Title = "RSI", YAxisKey = "RSIAxis", Color = OxyColors.Orange, StrokeThickness = 2 };
            MyPlotModel.Series.Add(rsiSeries);

            MyPlotView.Model = MyPlotModel;
        }

        private void GenerateInitialData()
        {
            pricePoints.Clear();
            volumePoints.Clear();
            double price = 100;

            DateTime now = DateTime.Now;

            for (int i = 0; i < maxPoints; i++)
            {
                price += rng.Next(-3, 4);
                pricePoints.Add(new DataPoint(DateTimeAxis.ToDouble(now.AddSeconds(-maxPoints + i)), price));
                volumePoints.Add(rng.Next(50, 150));
            }

            UpdateChart();
        }

        private void StartLiveDummyData()
        {
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // New price
            double lastPrice = pricePoints.Last().Y;
            double newPrice = lastPrice + rng.Next(-3, 4);
            DateTime now = DateTime.Now;

            pricePoints.Add(new DataPoint(DateTimeAxis.ToDouble(now), newPrice));
            volumePoints.Add(rng.Next(50, 150));

            // Keep last maxPoints
            if (pricePoints.Count > maxPoints)
            {
                pricePoints.RemoveAt(0);
                volumePoints.RemoveAt(0);
            }

            UpdateChart();
        }

        private void UpdateChart()
        {
            priceSeries.Points.Clear();
            smaSeries.Points.Clear();
            bollingerArea.Points.Clear();
            bollingerArea.Points2.Clear();
            volumeSeries.Items.Clear();
            rsiSeries.Points.Clear();

            for (int i = 0; i < pricePoints.Count; i++)
            {
                var pt = pricePoints[i];
                priceSeries.Points.Add(pt);

                // Volume
                volumeSeries.Items.Add(new RectangleBarItem(
                    pt.X - 0.4 / 86400, 0, pt.X + 0.4 / 86400, volumePoints[i]
                ));

                if (i >= smaPeriod - 1)
                {
                    var window = pricePoints.Skip(i - smaPeriod + 1).Take(smaPeriod).Select(p => p.Y).ToList();
                    double sma = window.Average();
                    smaSeries.Points.Add(new DataPoint(pt.X, sma));

                    double stddev = Math.Sqrt(window.Sum(v => Math.Pow(v - sma, 2)) / (smaPeriod - 1));
                    double upper = sma + bollingerMultiplier * stddev;
                    double lower = sma - bollingerMultiplier * stddev;
                    bollingerArea.Points.Add(new DataPoint(pt.X, upper));
                    bollingerArea.Points2.Add(new DataPoint(pt.X, lower));

                    // RSI
                    double gains = 0, losses = 0;
                    for (int j = 1; j < window.Count; j++)
                    {
                        double diff = window[j] - window[j - 1];
                        if (diff > 0) gains += diff;
                        else losses -= diff;
                    }
                    double rs = losses == 0 ? 100 : gains / losses;
                    double rsi = 100 - (100 / (1 + rs));
                    rsiSeries.Points.Add(new DataPoint(pt.X, rsi));
                }
            }

            MyPlotModel.InvalidatePlot(true);
        }
    }
}
