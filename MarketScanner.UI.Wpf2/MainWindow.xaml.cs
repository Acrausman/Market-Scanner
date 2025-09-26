using System.Collections.Generic;
using System.Windows;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Axes;
using OxyPlot.Annotations;

namespace MarketScanner.UI
{
    public partial class MainWindow : Window
    {
        public PlotModel MyPlotModel { get; private set; }
        private Dictionary<string, LineSeries> priceSeriesMap;
        private Dictionary<string, LineSeries> rsiSeriesMap;
        private MarketDataEngine engine;

        //RSI thresholds
        private const double RSIOverbought = 70;
        private const double RSIOversold = 30;

        public MainWindow()
        {
            InitializeComponent();

            MyPlotModel = new PlotModel { Title = "Market Scanner - Price + RSI" };

            // Configure dual axes
            var priceAxis = new LinearAxis { Position = AxisPosition.Left, Title = "Price" };
            var rsiAxis = new LinearAxis { Position = AxisPosition.Right, Title = "RSI", Minimum = 0, Maximum = 100 };
            MyPlotModel.Axes.Add(priceAxis);
            MyPlotModel.Axes.Add(rsiAxis);

            myPlot.Model = MyPlotModel;

            priceSeriesMap = new Dictionary<string, LineSeries>();
            rsiSeriesMap = new Dictionary<string, LineSeries>();

            // Symbols
            var symbols = new List<string> { "AAPL", "MSFT", "NVDA" };
            engine = new MarketDataEngine(symbols);

            // Create price and RSI series for each symbol
            foreach (var s in symbols)
            {
                var pSeries = new LineSeries
                {
                    Title = s + " Price",
                    YAxisKey = priceAxis.Key,
                    MarkerType = MarkerType.Circle
                };
                priceSeriesMap[s] = pSeries;
                MyPlotModel.Series.Add(pSeries);

                var rSeries = new LineSeries
                {
                    Title = s + " RSI",
                    YAxisKey = rsiAxis.Key,
                    MarkerType = MarkerType.None
                };
                rsiSeriesMap[s] = rSeries;
                MyPlotModel.Series.Add(rSeries);
            }

            //Add shaded overbought/oversold bands for RSI

            MyPlotModel.Annotations.Add(new RectangleAnnotation
            {
                MinimumY = RSIOverbought,
                MaximumY = 100,
                Fill = OxyColor.FromAColor(40, OxyColors.Red),
                Layer = AnnotationLayer.BelowSeries
            });

            MyPlotModel.Annotations.Add(new RectangleAnnotation
            {
                MinimumY = 0,
                MaximumY = RSIOversold,
                Fill = OxyColor.FromAColor(40, OxyColors.Green),
                Layer = AnnotationLayer.BelowSeries
            });

            engine.OnNewPrice += Engine_OnNewPrice;
            engine.OnNewRSI += Engine_OnNewRSI;
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

        private void Engine_OnTrigger(TriggerHit hit)
        {
            Dispatcher.Invoke(() =>
            {
                var annotation = new PointAnnotation
                {
                    X = priceSeriesMap[hit.Symbol].Points.Count - 1,
                    Y = hit.Price,
                    Shape = hit.TriggerName == "TrendLong" ? MarkerType.Triangle : MarkerType.Square,
                    Fill = hit.TriggerName == "TrendLong" ? OxyColors.Green : OxyColors.Red,
                    Stroke = OxyColors.Black,
                    StrokeThickness = 1
                };
                MyPlotModel.Annotations.Add(annotation);

                //  Highlight a small zone for triggers
                var rect = new RectangleAnnotation
                {
                    MinimumX = annotation.X - 0.5,
                    MaximumX = annotation.X + 0.5,
                    MinimumY = annotation.Y - 0.5,
                    MaximumY = annotation.Y + 0.5,
                    Fill = hit.TriggerName == "TrendLong" ? OxyColor.FromAColor(60, OxyColors.Green) :
                                                            OxyColor.FromAColor(60, OxyColors.Red),

                    Layer = AnnotationLayer.BelowSeries
                };
                MyPlotModel.Annotations.Add(rect);

                MyPlotModel.InvalidatePlot(true);
            });
        }
    }
}
