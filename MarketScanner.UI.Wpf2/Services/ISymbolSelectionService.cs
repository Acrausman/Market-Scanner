using System.Threading.Tasks;

namespace MarketScanner.UI.Wpf.Services
{
    public interface ISymbolSelectionService
    {
        Task SelectSymbolAsync(string? symbol);
    }
}
