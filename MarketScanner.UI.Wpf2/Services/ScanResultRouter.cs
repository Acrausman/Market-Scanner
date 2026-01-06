using System.Linq;
using System.Windows.Threading;
using MarketScanner.Core.Models;
using MarketScanner.Data.Diagnostics;
using MarketScanner.UI.Wpf.ViewModels;

namespace MarketScanner.UI.Wpf.Services
{
    public class ScanResultRouter : IScanResultRouter
    {
        private readonly AlertPanelViewModel _alerts;
        private readonly Dispatcher _dispatcher;

        public ScanResultRouter(AlertPanelViewModel alerts, Dispatcher dispatcher)
        {
            _alerts = alerts;
            _dispatcher = dispatcher;
        }

        public void HandleResult(EquityScanResult result)
        {
            _dispatcher.Invoke(() =>
            {
                bool isOverbought = result.IsOverbought || result.Tags.Contains("Overbought");
                bool isOversold = result.IsOversold || result.Tags.Contains("Oversold");
                bool isCreeper = result.IsCreeper || result.Tags.Contains("Creeper");

                if (isOverbought)
                {
                    if (!_alerts.OversoldSymbols.Contains(result.Symbol))
                        _alerts.OverboughtSymbols.Add(result.Symbol);
                }
                if(isOversold)
                {
                    if (!_alerts.OversoldSymbols.Contains(result.Symbol))
                        _alerts.OversoldSymbols.Add(result.Symbol);
                }
                if(isCreeper)
                {
                    if (!_alerts.CreeperSymbols.Contains(result.Symbol))
                        Logger.WriteLine($"Creeper...aw man");
                        _alerts.CreeperSymbols.Add(result.Symbol);
                }

            });
        }
    }
}
