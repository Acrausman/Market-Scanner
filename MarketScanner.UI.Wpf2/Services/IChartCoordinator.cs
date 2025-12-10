using System.Threading.Tasks;
using MarketScanner.Core.Models;

namespace MarketScanner.UI.Wpf.Services
{
    public interface IChartCoordinator
    {
        Task OnSymbolSelected(string? symbol);
        Task OnScanResult(EquityScanResult result);
    }
}
