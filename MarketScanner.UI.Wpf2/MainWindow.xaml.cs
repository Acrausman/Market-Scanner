using MarketScanner.UI;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Windows;

public class MarketChartViewModel : Window
{
    public PlotModel PriceVolumePlot { get; private set; }
    public PlotModel RsiPlot { get; private set; }

    private LineSeries priceSeries, smaSeries;
    private AreaSeries bollingerSeries;
    private RectangleBarSeries volumeSeries;
    private LineSeries rsiSeries;

    private MarketDataEngine engine;

    public MarketChartViewModel(MarketDataEngine marketEngine)
    {
        engine = marketEngine;
        SetupPlots();

        // subscribe to engine events
        engine.OnNewPrice += HandleNewPrice;
        engine.OnNewVolume += HandleNewVolume;
        engine.OnNewSMA += HandleNewSMA;
        engine.OnNewRSI += HandleNewRSI;
    }

    private void SetupPlots()
    {
        PriceVolumePlot = new PlotModel { Title = "Market Data" };
        PriceVolumePlot.Axes.Add(new DateTimeAxis
        {
            Position = AxisPosition.Bottom,
            StringFormat = "HH:mm:ss"
        });
        PriceVolumePlot.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = "Price"
        });
        PriceVolumePlot.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Right,
            Title = "Volume",
            Key = "VolumeAxis"
        });

        priceSeries = new LineSeries { Title = "Price", Color = OxyColors.SteelBlue };
        smaSeries = new LineSeries { Title = "SMA", Color = OxyColors.Red };
        bollingerSeries = new AreaSeries
        {
            Title = "Bollinger",
            Fill = OxyColor.FromAColor(60, OxyColors.Green)
        };
        volumeSeries = new RectangleBarSeries
        {
            Title = "Volume",
            YAxisKey = "VolumeAxis",
            FillColor = OxyColors.Gray
        };

        PriceVolumePlot.Series.Add(priceSeries);
        PriceVolumePlot.Series.Add(smaSeries);
        PriceVolumePlot.Series.Add(bollingerSeries);
        PriceVolumePlot.Series.Add(volumeSeries);

        RsiPlot = new PlotModel { Title = "RSI" };
        RsiPlot.Axes.Add(new DateTimeAxis { Position = AxisPosition.Bottom, StringFormat = "HH:mm:ss" });
        RsiPlot.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Minimum = 0, Maximum = 100 });
        rsiSeries = new LineSeries { Title = "RSI", Color = OxyColors.Purple };
        RsiPlot.Series.Add(rsiSeries);
    }

    private void HandleNewPrice(string symbol, double price)
    {
        double x = DateTimeAxis.ToDouble(DateTime.Now);
        priceSeries.Points.Add(new DataPoint(x, price));
        TrimSeries(priceSeries.Points, 50);
        PriceVolumePlot.InvalidatePlot(true);
    }

    private void HandleNewVolume(string symbol, double volume)
    {
        double x = DateTimeAxis.ToDouble(DateTime.Now);
        volumeSeries.Items.Add(new RectangleBarItem
        {
            X0 = x - 0.0005,
            X1 = x + 0.0005,
            Y0 = 0,
            Y1 = volume
        });
        TrimSeries(volumeSeries.Items, 50);
        PriceVolumePlot.InvalidatePlot(true);
    }

    private void HandleNewSMA(string symbol, double sma, double upper, double lower)
    {
        double x = DateTimeAxis.ToDouble(DateTime.Now);
        smaSeries.Points.Add(new DataPoint(x, sma));
        bollingerSeries.Points.Add(new DataPoint(x, upper));
        bollingerSeries.Points2.Add(new DataPoint(x, lower));
        TrimSeries(smaSeries.Points, 50);
        TrimSeries(bollingerSeries.Points, 50);
        RsiPlot.InvalidatePlot(true);
    }

    private void HandleNewRSI(string symbol, double rsi)
    {
        double x = DateTimeAxis.ToDouble(DateTime.Now);
        rsiSeries.Points.Add(new DataPoint(x, rsi));
        TrimSeries(rsiSeries.Points, 50);
        RsiPlot.InvalidatePlot(true);
    }

    private void TrimSeries(IList<DataPoint> points, int max)
    {
        while (points.Count > max) points.RemoveAt(0);
    }

    private void TrimSeries(IList<RectangleBarItem> items, int max)
    {
        while (items.Count > max) items.RemoveAt(0);
    }
}
