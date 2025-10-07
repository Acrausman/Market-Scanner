using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Axes;
using System.Collections.Generic;

namespace MarketScanner.UI.Wpf.Services
{
    public class ChartManager
    {
        public PlotModel PriceView { get; private set; }
        public PlotModel RsiView { get; private set; }
        public PlotModel VolumeView { get; private set; }

        private LineSeries priceSeries;
        private LineSeries smaSeries;
        private AreaSeries bollingerBands;
        private LineSeries rsiSeries;
        private RectangleBarSeries volumeSeries;

        public ChartManager()
        {
            //Initialize all 3 charts
            PriceView = CreateBaseChart("Price");
            RsiView = CreateBaseChart("RSI");
            VolumeView = CreateBaseChart("Volume");

            //Add series
            priceSeries = CreatePriceSeries();
            smaSeries = CreateSmaSeries();
            bollingerBands = CreateBollingerBandSeries();
            rsiSeries = new LineSeries { Title = "RSI", Color = OxyColors.Goldenrod };
            volumeSeries = new RectangleBarSeries { Title = "Volume", FillColor = OxyColors.SteelBlue };

            PriceView.Series.Add(bollingerBands);
            PriceView.Series.Add(priceSeries);
            PriceView.Series.Add(smaSeries);
            RsiView.Series.Add(rsiSeries);
            VolumeView.Series.Add(volumeSeries);

        }

        public PlotModel CreateBaseChart(string title)
        {
            var model = new PlotModel { Title = title };

            model.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "MM/dd",
                Title = "Date",
                MajorGridlineStyle = LineStyle.Dot
            });

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = title.Contains("RSI") ? "RSI" :
                        title.Contains("Volume") ? "Volume" : "Price",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot
            });

            return model;
        }


        public LineSeries CreatePriceSeries() => new()
        {
            Title = "Price",
            Color = OxyColors.SteelBlue,
            StrokeThickness = 2
        };

        public LineSeries CreateSmaSeries() => new()
        {
            Title = "SMA",
            Color = OxyColors.Red,
            StrokeThickness = 2
        };

        public AreaSeries CreateBollingerBandSeries() => new()
        {
            Title = "Bollinger Bands",
            Color = OxyColor.FromAColor(80, OxyColors.LightGreen),
            Fill = OxyColor.FromAColor(40, OxyColors.LightGreen)
        };

        public void UpdatePriceData(
            List<DataPoint> pricePoints,
            List<DataPoint> smaPoints,
            List<(DataPoint upper, DataPoint lower)> bands)
        {
            priceSeries.Points.Clear();
            smaSeries.Points.Clear();
            bollingerBands.Points.Clear();
            bollingerBands.Points2.Clear();

            foreach(var pt in pricePoints)
                priceSeries.Points.Add(pt);
            foreach (var pt in smaPoints)
                smaSeries.Points.Add(pt);
            foreach (var(upper, lower) in bands)
            {
                bollingerBands.Points.Add(upper);
                bollingerBands.Points2.Add(lower);
            }

            PriceView.InvalidatePlot(true);
        }

        public void UpdateRsiData(List<DataPoint> rsiPoints)
        {
            rsiSeries.Points.Clear();
            rsiSeries.Points.AddRange(rsiPoints);
            RsiView.InvalidatePlot(true);
        }

        public void UpdateVolumeData(List<DataPoint> volumePoints)
        {
            volumeSeries.Items.Clear();
            foreach (var pt in volumePoints)
            {
                double barWidth = 0.5;
                volumeSeries.Items.Add(new RectangleBarItem(pt.X - barWidth / 2, 0, pt.X + barWidth / 2, pt.Y));
            }
            VolumeView.InvalidatePlot(true);
        }

        public void ClearAllSeries()
        {
            priceSeries.Points.Clear();
            smaSeries.Points.Clear();
            bollingerBands.Points.Clear();
            bollingerBands.Points2.Clear();
            rsiSeries.Points.Clear();
            volumeSeries.Items.Clear();

            PriceView.InvalidatePlot(true);
            RsiView.InvalidatePlot(true);
            VolumeView.InvalidatePlot(true);
        }
    }
}
