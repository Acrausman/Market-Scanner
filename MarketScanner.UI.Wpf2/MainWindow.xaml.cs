using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Collections.Generic;
using System.Windows;

namespace MarketScanner.UI
{
    public partial class MainWindow : Window
    {
        public PlotModel MyPlotModel { get; private set; }
        private Dictionary<string, LineSeries> priceSeriesMap;
        private Dictionary<string, LineSeries> rsiSeriesMap;
        private Dictionary<string, LineSeries> smaSeriesMap;
        private Dictionary<string, LineSeries> upperBandSeriesMap;
        private Dictionary<string, LineSeries> lowerBandSeriesMap;
        private Dictionary<string, LineSeries> volumeSeriesMap;
        private MarketDataEngine engine;

        private const double RSIOverbought = 70;
        private const double RSIOversold = 30;

        public MainWindow()
        {
            InitializeComponent();

            MyPlotModel = new PlotModel { Title = "Market Scanner - Price + RSI + SMA + Volume" };

            // Axes
            var priceAxis = new LinearAxis { Position = AxisPosition.Left, Title = "Price" };
            var rsiAxis = new LinearAxis { Position = AxisPosition.Right, Title = "RSI", Minimum = 0, Maximum = 100 };
            var volAxis = new LinearAxis { Position = AxisPosition.Bottom, Title = "Volume", Minimum = 0, Key = "VolumeAxis" };
            MyPlotModel.Axes.Add(priceAxis);
            MyPlotModel.Axes.Add(rsiAxis);

            myPlot.Model = MyPlotModel;

            priceSeriesMap = new Dictionary<string, LineSeries>();
            rsiSeriesMap = new Dictionary<string, LineSeries>();
            smaSeriesMap = new Dictionary<string, LineSeries>();
            upperBandSeriesMap = new Dictionary<string, LineSeries>();
            lowerBandSeriesMap = new Dictionary<string, LineSeries>();
            volumeSeriesMap = new Dictionary<string, LineSeries>();

            var symbols = new List<string> { "AAPL", "MSFT", "NVDA" };
            engine = new MarketDataEngine(symbols);

            foreach (var s in symbols)
            {
                // Price
                var pSeries = new LineSeries { Title = s + " Price", YAxisKey = priceAxis.Key };
                priceSeriesMap[s] = pSeries;
                MyPlotModel.Series.Add(pSeries);

                // RSI
                var rSeries = new LineSeries { Title = s + " RSI", YAxisKey = rsiAxis.Key };
                rsiSeriesMap[s] = rSeries;
                MyPlotModel.Series.Add(rSeries);

                // SMA and bands
                var smaSeries = new LineSeries { Title = s + " SMA14", YAxisKey = priceAxis.Key, Color = OxyColors.Orange };
                var upperSeries = new LineSeries { Title = s + " Upper Band", YAxisKey = priceAxis.Key, Color = OxyColors.LightBlue };
                var lowerSeries = new LineSeries { Title = s + " Lower Band", YAxisKey = priceAxis.Key, Color = OxyColors.LightBlue };
                smaSeriesMap[s] = smaSeries;
                upperBandSeriesMap[s] = upperSeries;
                lowerBandSeriesMap[s] = lowerSeries;
                MyPlotModel.Series.Add(smaSeries);
                MyPlotModel.Series.Add(upperSeries);
                MyPlotModel.Series.Add(lowerSeries);

                // Volume
                var vSeries = new LineSeries { Title = s + " Volume", YAxisKey = volAxis.Key };
                volumeSeriesMap[s] = vSeries;
                //MyPlotModel.Series.Add(vSeries);
            }

            // RSI Bands
            MyPlotModel.Annotations.Add(new RectangleAnnotation { MinimumY = RSIOverbought, MaximumY = 100, Fill = OxyColor.FromAColor(40, OxyColors.Red) });
            MyPlotModel.Annotations.Add(new RectangleAnnotation { MinimumY = 0, MaximumY = RSIOversold, Fill = OxyColor.FromAColor(40, OxyColors.Green) });

            // Event wiring
            engine.OnNewPrice += Engine_OnNewPrice;
            engine.OnNewRSI += Engine_OnNewRSI;
            engine.OnNewSMA += Engine_OnNewSMA;
            engine.OnTrigger += Engine_OnTrigger;

            engine.Start();
        }

        private void Engine_OnNewPrice(string symbol, double price)
        {
            Dispatcher.Invoke(() =>
            {
                var series = priceSeriesMap[symbol];
                series.Points.Add(new DataPoint(series.Points.Count, price));
                MyPlotModel.InvalidatePlot(true);
            });
        }

        private void Engine_OnNewRSI(string symbol, double rsi)
        {
            Dispatcher.Invoke(() =>
            {
                var series = rsiSeriesMap[symbol];
                series.Points.Add(new DataPoint(series.Points.Count, rsi));
                MyPlotModel.InvalidatePlot(true);
            });
        }

        private void Engine_OnNewSMA(string symbol, double sma, double upper, double lower)
        {
            Dispatcher.Invoke(() =>
            {
                smaSeriesMap[symbol].Points.Add(new DataPoint(smaSeriesMap[symbol].Points.Count, sma));
                upperBandSeriesMap[symbol].Points.Add(new DataPoint(upperBandSeriesMap[symbol].Points.Count, upper));
                lowerBandSeriesMap[symbol].Points.Add(new DataPoint(lowerBandSeriesMap[symbol].Points.Count, lower));
                MyPlotModel.InvalidatePlot(true);
            });
        }

        private void Engine_OnTrigger(TriggerHit hit)
        {
            Dispatcher.Invoke(() =>
            {
                var annotation = new PointAnnotation
                {
                    X = priceSeriesMap[hit.Symbol].Points.Count - 1,
                    Y = hit.Price,
                    Shape = hit.TriggerName == "TrendLong" ? MarkerType.Triangle : MarkerType.Square,
                    Fill = hit.TriggerName == "TrendLong" ? OxyColors.Green : OxyColors.Red
                };
                MyPlotModel.Annotations.Add(annotation);
                MyPlotModel.InvalidatePlot(true);
            });
        }
    }
}
