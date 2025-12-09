using System.Threading.Tasks;
using System.Windows.Threading;
using MarketScanner.Core.Metadata;
using MarketScanner.Core.Models;
using MarketScanner.Data.Services;
using MarketScanner.Data.Services.Analysis;
using MarketScanner.UI.Wpf.ViewModels;

namespace MarketScanner.UI.Wpf.Services
{
    public class SymbolSelectionService : ISymbolSelectionService
    {
        private readonly EquityScannerService _scannerService;
        private readonly ChartViewModel _chartViewModel;

        public SymbolSelectionService(
            EquityScannerService scannerService,
            ChartViewModel chartViewModel)
        {
            _scannerService = scannerService;
            _chartViewModel = chartViewModel;
        }

        public async Task SelectSymbolAsync(string? symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return;

            if(_scannerService.ScanCache.TryGetValue(symbol, out EquityScanResult? result))
            {
                _chartViewModel.Update(result);
            }
            else
            {
                _chartViewModel.Clear();
            }

            await _chartViewModel.LoadChartForSymbol(symbol);
        }
    }
}
