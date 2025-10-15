using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MarketScanner.UI.Wpf.Services
{
    public class ChartManager
    {
        public PlotModel PriceView { get; private set; }
        public PlotModel RsiView { get; private set; }
        public PlotModel VolumeView { get; private set; }

        private LineSeries _priceSeries;
        private LineSeries _smaSeries;
        private AreaSeries _bollingerSeries;
        private LineSeries _rsiSeries;
        private LineSeries _volumeSeries;

        public ChartManager()
        {
            InitializePriceView();
            InitializeRsiView();
            InitializeVolumeView();
        }

        // --- PRICE + SMA + BOLLINGER ---
        private void InitializePriceView()
        {
            PriceView = new PlotModel { Title = "Price & SMA", PlotAreaBorderThickness = new OxyThickness(1) };
            PriceView.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Date",
                StringFormat = "MM-dd",
                IntervalType = DateTimeIntervalType.Days,
                MinorIntervalType = DateTimeIntervalType.Days,
                IsZoomEnabled = true,
                IsPanEnabled = true
            });
            PriceView.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Price" });

            _priceSeries = new LineSeries { Title = "Price", Color = OxyColors.SteelBlue, StrokeThickness = 2 };
            _smaSeries = new LineSeries { Title = "SMA14", Color = OxyColors.OrangeRed, StrokeThickness = 2 };
            _bollingerSeries = new AreaSeries
            {
                Title = "Bollinger Bands",
                Color = OxyColor.FromAColor(60, OxyColors.LightSkyBlue),
                Fill = OxyColor.FromAColor(60, OxyColors.LightSkyBlue)
            };

            PriceView.Series.Add(_bollingerSeries);
            PriceView.Series.Add(_smaSeries);
            PriceView.Series.Add(_priceSeries);
        }

        // --- RSI ---
        private void InitializeRsiView()
        {
            RsiView = new PlotModel { Title = "RSI (14)", PlotAreaBorderThickness = new OxyThickness(1) };
            RsiView.Axes.Add(new DateTimeAxis { Position = AxisPosition.Bottom, StringFormat = "HH:mm:ss", Title = "Time" });
            RsiView.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "RSI", Minimum = 0, Maximum = 100 });

            _rsiSeries = new LineSeries { Title = "RSI", Color = OxyColors.MediumPurple, StrokeThickness = 2 };
            RsiView.Series.Add(_rsiSeries);

            // Add reference lines for Overbought (70) and Oversold (30)
            RsiView.Annotations.Add(new LineAnnotation { Y = 70, Color = OxyColors.Red, LineStyle = LineStyle.Dash });
            RsiView.Annotations.Add(new LineAnnotation { Y = 30, Color = OxyColors.Green, LineStyle = LineStyle.Dash });
        }

        // --- VOLUME ---
        private void InitializeVolumeView()
        {
            VolumeView = new PlotModel { Title = "Volume", PlotAreaBorderThickness = new OxyThickness(1) };
            VolumeView.Axes.Add(new DateTimeAxis { Position = AxisPosition.Bottom, StringFormat = "HH:mm:ss", Title = "Time" });
            VolumeView.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Volume" });

            _volumeSeries = new LineSeries { Title = "Volume", Color = OxyColors.Gray, StrokeThickness = 1.5 };
            VolumeView.Series.Add(_volumeSeries);
        }

        // --- Update Methods ---
        public void UpdatePriceData(
            List<DataPoint> pricePoints,
            List<DataPoint> smaPoints,
            List<(DataPoint upper, DataPoint lower)> bollingerPoints)
        {
            _priceSeries.Points.AddRange(pricePoints);
            _smaSeries.Points.AddRange(smaPoints);

            foreach (var (upper, lower) in bollingerPoints)
            {
                _bollingerSeries.Points.Add(upper);
                _bollingerSeries.Points2.Add(lower);
            }

            PriceView.InvalidatePlot(true);
        }

        public void UpdateRsiData(List<DataPoint> rsiPoints)
        {
            _rsiSeries.Points.AddRange(rsiPoints);
            RsiView.InvalidatePlot(true);
        }

        public void UpdateVolumeData(List<DataPoint> volumePoints)
        {
            _volumeSeries.Points.AddRange(volumePoints);
            VolumeView.InvalidatePlot(true);
        }

        public void AddPricePoint(double price)
        {
            if (_priceSeries == null) return;
            var now = DateTimeAxis.ToDouble(DateTime.Now);
            _priceSeries.Points.Add(new DataPoint(now, price));
            if (_priceSeries.Points.Count > 300)
                _priceSeries.Points.RemoveAt(0);
            PriceView.InvalidatePlot(true);
        }

        public void AddVolumePoint(double volume)
        {
            if (_volumeSeries == null) return;
            var now = DateTimeAxis.ToDouble(DateTime.Now);
            _volumeSeries.Points.Add(new DataPoint(now, volume));
            if (_volumeSeries.Points.Count > 300)
                _volumeSeries.Points.RemoveAt(0);
            VolumeView.InvalidatePlot(true);
        }

        public void AddRsiPoint(double rsi)
        {
            if (_rsiSeries == null) return;
            var now = DateTimeAxis.ToDouble(DateTime.Now);
            _rsiSeries.Points.Add(new DataPoint(now, rsi));
            if (_rsiSeries.Points.Count > 300)
                _rsiSeries.Points.RemoveAt(0);
            RsiView.InvalidatePlot(true);
        }

        public void UpdateSmaBands(double sma, double upper, double lower)
        {
            if (_smaSeries == null || _bollingerSeries == null) return;
            var now = DateTimeAxis.ToDouble(DateTime.Now);

            _smaSeries.Points.Add(new DataPoint(now, sma));
            _bollingerSeries.Points.Add(new DataPoint(now, upper));
            _bollingerSeries.Points2.Add(new DataPoint(now, lower));

            if (_smaSeries.Points.Count > 300)
            {
                _smaSeries.Points.RemoveAt(0);
                _bollingerSeries.Points.RemoveAt(0);
                _bollingerSeries.Points2.RemoveAt(0);
            }

            PriceView.InvalidatePlot(true);
        }


        public void ClearAllSeries()
        {
            _priceSeries.Points.Clear();
            _smaSeries.Points.Clear();
            _bollingerSeries.Points.Clear();
            _bollingerSeries.Points2.Clear();
            _rsiSeries.Points.Clear();
            _volumeSeries.Points.Clear();

            PriceView.InvalidatePlot(true);
            RsiView.InvalidatePlot(true);
            VolumeView.InvalidatePlot(true);
        }
    }
}
