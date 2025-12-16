using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using MarketScanner.Core.Models;
using MarketScanner.UI.Wpf.ViewModels;

namespace MarketScanner.UI.Wpf.Services
{
    public class ChartCoordinator : IChartCoordinator
    {
        private readonly ChartViewModel _chartView;
        private readonly Dispatcher _dispatcher;
        private string? _currentSymbol;
        private bool _isLoadingHistory = false;

        public ChartCoordinator(ChartViewModel chartView, Dispatcher dispatcher)
        {
            _chartView = chartView;
            _dispatcher = dispatcher;
        }
        
        public async Task OnSymbolSelected(string? symbol)
        {
            _currentSymbol = symbol;
            if (string.IsNullOrWhiteSpace(symbol))
                return;
            _isLoadingHistory = true;
            await _chartView.LoadChartForSymbol(symbol);
            _isLoadingHistory = false;
        }

        public async Task OnScanResult(EquityScanResult result)
        {
            if (!string.Equals(result.Symbol, _currentSymbol))
                return;
            if (_isLoadingHistory)
                return;
            await _dispatcher.InvokeAsync(() =>
            {
                _chartView.Update(result);
            });
        }
    }
}
