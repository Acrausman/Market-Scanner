using OxyPlot;
using OxyPlot.Series;
using System.Collections.Generic;

namespace MarketScanner.UI.Wpf.Services
{
    public interface IChartService
    {
        PlotModel PriceView { get; }
        PlotModel RsiView { get; }
        PlotModel VolumeView { get; }

        void ClearAllSeries();
        void UpdatePriceData(IReadOnlyList<DataPoint> pricePoints,
                             IReadOnlyList<DataPoint> smaPoints,
                             IReadOnlyList<(DataPoint upper, DataPoint lower)> bollingerPoints,
                             bool isLive = false);
        void UpdateRsiData(IReadOnlyList<DataPoint> rsiPoints);
        void UpdateVolumeData(IReadOnlyList<DataPoint> volumePoints);
    }
}
