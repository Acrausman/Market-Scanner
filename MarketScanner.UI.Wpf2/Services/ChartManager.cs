using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Legends;
using System.Collections.Generic;
using System.Linq;

namespace MarketScanner.UI.Wpf.Services
{
    public class ChartManager : IChartService
    {
        public PlotModel PriceView { get; }
        public PlotModel RsiView { get; }
        public PlotModel VolumeView { get; }

        private readonly LineSeries _priceSeries;
        private readonly LineSeries _smaSeries;
        private readonly AreaSeries _bollingerSeries;
        private readonly LineSeries _rsiSeries;
        private readonly LineSeries _volumeSeries;

        public ChartManager()
        {
            PriceView = CreatePriceView(out _priceSeries, out _smaSeries, out _bollingerSeries);
            RsiView = CreateRsiView(out _rsiSeries);
            VolumeView = CreateVolumeView(out _volumeSeries);
        }

        public void ClearAllSeries()
        {
            _priceSeries.Points.Clear();
            _smaSeries.Points.Clear();
            _bollingerSeries.Points.Clear();
            _bollingerSeries.Points2.Clear();
            _rsiSeries.Points.Clear();
            _volumeSeries.Points.Clear();

            _priceSeries.TrackerFormatString = "Date: {2:MM-dd}\nPrice: {4:F2}";
            _smaSeries.TrackerFormatString = "Date: {2:MM-dd}\nSMA: {4:F2}";
            _volumeSeries.TrackerFormatString = "Date: {2:MM-dd}\nVolume: {4:N0}";

            PriceView.InvalidatePlot(true);
            RsiView.InvalidatePlot(true);
            VolumeView.InvalidatePlot(true);
        }

        public void UpdatePriceData(IReadOnlyList<DataPoint> pricePoints,
                            IReadOnlyList<DataPoint> smaPoints,
                            IReadOnlyList<(DataPoint upper, DataPoint lower)> bollingerPoints,
                            bool isLive = false)
        {
            // Always clear existing series — ensures full redraw
            PriceView.Series.Clear();

            // --- Price line ---
            var priceSeries = new LineSeries
            {
                Title = "Price",
                Color = OxyColors.SteelBlue,
                StrokeThickness = 2
            };
            priceSeries.Points.AddRange(pricePoints);
            PriceView.Series.Add(priceSeries);

            // --- SMA line ---
            var smaSeries = new LineSeries
            {
                Title = "SMA14",
                Color = OxyColors.OrangeRed,
                StrokeThickness = 2
            };
            smaSeries.Points.AddRange(smaPoints.Where(p => !double.IsNaN(p.Y)));
            PriceView.Series.Add(smaSeries);

            // --- Bollinger Bands ---
            if (bollingerPoints != null && bollingerPoints.Count > 0)
            {
                var areaSeries = new AreaSeries
                {
                    Title = "Bollinger Bands",
                    Color = OxyColor.FromAColor(60, OxyColors.LightSkyBlue),
                    Fill = OxyColor.FromAColor(60, OxyColors.LightSkyBlue)
                };

                foreach (var (upper, lower) in bollingerPoints)
                {
                    areaSeries.Points.Add(upper);
                    areaSeries.Points2.Add(lower);
                }

                PriceView.Series.Add(areaSeries);
            }

            PriceView.InvalidatePlot(true);
            AdjustPriceAxis();
        }


        public void UpdateRsiData(IReadOnlyList<DataPoint> rsiPoints)
        {
            _rsiSeries.Points.Clear();
            _rsiSeries.Points.AddRange(rsiPoints);

            RsiView.Annotations.Clear();

            RsiView.Annotations.Add(new LineAnnotation { Y = 70, Color = OxyColors.Red, LineStyle = LineStyle.Dash});
            RsiView.Annotations.Add(new LineAnnotation { Y = 30, Color = OxyColors.Green, LineStyle = LineStyle.Dash});

            foreach (var p in rsiPoints)
            {
                if(p.Y >= 70)
                {
                    RsiView.Annotations.Add(new PointAnnotation
                    {
                        X = p.X,
                        Y = p.Y,
                        Shape = MarkerType.Triangle,
                        TextColor = OxyColors.Red,
                        Size = 5,
                        ToolTip = $"Overbought RSI={p.Y:F1} ({DateTimeAxis.ToDateTime(p.X):MM/dd})"
                    });
                }
                else if (p.Y <= 30)
                {
                    RsiView.Annotations.Add(new PointAnnotation
                    {
                        X = p.X,
                        Y = p.Y,
                        Shape = MarkerType.Triangle,
                        TextColor = OxyColors.Green,
                        Size = 5,
                        ToolTip = $"Oversold RSI={p.Y:F1} ({DateTimeAxis.ToDateTime(p.X):MM/dd})"
                    });
                }
            }

            RsiView.InvalidatePlot(true);
        }

        public void UpdateVolumeData(IReadOnlyList<DataPoint> volumePoints)
        {
            _volumeSeries.Points.Clear();
            _volumeSeries.Points.AddRange(volumePoints);
            VolumeView.InvalidatePlot(true);
        }

        private static PlotModel CreatePriceView(out LineSeries priceSeries,
                                                 out LineSeries smaSeries,
                                                 out AreaSeries bollingerSeries)
        {
            var model = new PlotModel
            {
                Title = "Price & SMA",
                PlotAreaBorderThickness = new OxyThickness(1)
            };

            model.IsLegendVisible = true;
            model.Legends.Clear();
            model.Legends.Add(new Legend
            {
                LegendTitle = "",
                LegendPosition = LegendPosition.TopRight,
                LegendOrientation = LegendOrientation.Horizontal
            });

            model.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Date",
                StringFormat = "MM-dd",
                IntervalType = DateTimeIntervalType.Days,
                MinorIntervalType = DateTimeIntervalType.Days,
                IsZoomEnabled = true,
                IsPanEnabled = true,
                MinimumPadding = 0.05,
                MaximumPadding = 0.05
            });

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Price",
                MinimumPadding = 0.2,
                MaximumPadding = 0.2,
                IsZoomEnabled = true,
                IsPanEnabled = true
            });

            priceSeries = new LineSeries
            {
                Title = "Price",
                Color = OxyColors.SteelBlue,
                StrokeThickness = 2
            };

            smaSeries = new LineSeries
            {
                Title = "SMA14",
                Color = OxyColors.OrangeRed,
                StrokeThickness = 2
            };

            bollingerSeries = new AreaSeries
            {
                Title = "Bollinger Bands",
                Color = OxyColor.FromAColor(60, OxyColors.LightSkyBlue),
                Fill = OxyColor.FromAColor(60, OxyColors.LightSkyBlue)
            };

            model.Series.Add(bollingerSeries);
            model.Series.Add(smaSeries);
            model.Series.Add(priceSeries);
            return model;
        }

        private static PlotModel CreateRsiView(out LineSeries rsiSeries)
        {
            var model = new PlotModel
            {
                Title = "RSI (14)",
                PlotAreaBorderThickness = new OxyThickness(1)
            };

            model.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "MM-dd",
                Title = "Date",
                IntervalType = DateTimeIntervalType.Days,
                MinorIntervalType = DateTimeIntervalType.Days,
                IsZoomEnabled = true,
                IsPanEnabled = true
            });

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "RSI",
                Minimum = 0,
                Maximum = 100
            });

            rsiSeries = new LineSeries
            {
                Title = "RSI",
                Color = OxyColors.MediumPurple,
                StrokeThickness = 2
            };

            model.Series.Add(rsiSeries);
            model.Annotations.Add(new LineAnnotation { Y = 70, Color = OxyColors.Red, LineStyle = LineStyle.Dash });
            model.Annotations.Add(new LineAnnotation { Y = 30, Color = OxyColors.Green, LineStyle = LineStyle.Dash });
            return model;
        }

        private static PlotModel CreateVolumeView(out LineSeries volumeSeries)
        {
            var model = new PlotModel
            {
                Title = "Volume",
                PlotAreaBorderThickness = new OxyThickness(1)
            };

            model.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "MM-dd",
                Title = "Date",
                IntervalType = DateTimeIntervalType.Days,
                MinorIntervalType = DateTimeIntervalType.Days,
                IsZoomEnabled = true,
                IsPanEnabled = true
            });

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Volume"
            });

            volumeSeries = new LineSeries
            {
                Title = "Volume",
                Color = OxyColors.Gray,
                StrokeThickness = 1.5
            };

            model.Series.Add(volumeSeries);
            return model;
        }


        private static void TrimSeries(LineSeries series)
        {
            if (series.Points.Count <= 300)
            {
                return;
            }

            series.Points.RemoveRange(0, series.Points.Count - 300);
        }

        private static void TrimSeries(IList<DataPoint> series, IList<DataPoint> secondary)
        {
            if (series.Count <= 300)
            {
                return;
            }

            var remove = series.Count - 300;
            for (int i = 0; i < remove; i++)
            {
                series.RemoveAt(0);
                secondary.RemoveAt(0);
            }
        }

        private void AdjustPriceAxis()
        {
            if (_priceSeries.Points.Count == 0)
            {
                return;
            }

            var linearAxis = PriceView.Axes.OfType<LinearAxis>().FirstOrDefault();
            if (linearAxis == null)
            {
                return;
            }

            var min = _priceSeries.Points.Min(p => p.Y) * 0.95;
            var max = _priceSeries.Points.Max(p => p.Y) * 1.05;
            linearAxis.Zoom(min, max);
        }
    }
}
