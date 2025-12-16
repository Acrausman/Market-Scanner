using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketScanner.UI.Wpf.Services
{
    public interface ISymbolSelectionCoordinator
    {
        string? SelectedSymbol { get; }
        event Action<string?>? SymbolSelected;
        void SelectSymbol(string? symbol);
    }
}
