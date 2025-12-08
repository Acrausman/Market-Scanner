using System.Linq;
using System.Windows.Threading;
using MarketScanner.Core.Models;
using MarketScanner.UI.Wpf.ViewModels;

namespace MarketScanner.UI.Wpf.Services
{
    public class ScanResultRouter : IScanResultRouter
    {
        private readonly ScannerViewModel _scannerViewModel;
        private readonly Dispatcher _dispatcher;

        public ScanResultRouter(ScannerViewModel scannerViewModel, Dispatcher dispatcher)
        {
            _scannerViewModel = scannerViewModel;
            _dispatcher = dispatcher;
        }

        public void HandleResult(EquityScanResult result)
        {
            _dispatcher.Invoke(() =>
            {
                bool isOverbought = result.IsOverbought || result.Tags.Contains("Overbought");
                bool isOversold = result.IsOversold || result.Tags.Contains("Oversold");

                if (isOverbought)
                {
                    if (!_scannerViewModel.OversoldSymbols.Contains(result.Symbol))
                        _scannerViewModel.OverboughtSymbols.Add(result.Symbol);
                }
                if(isOversold)
                {
                    if (!_scannerViewModel.OversoldSymbols.Contains(result.Symbol))
                        _scannerViewModel.OversoldSymbols.Add(result.Symbol);
                }

                //Later: creepers and other criteria
            });
        }
    }
}
