using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketScanner.UI.Wpf.Services
{
    public class SymbolSelectionCoordinator : ISymbolSelectionCoordinator
    {
        private string? _selectedSymbol;
        public string? SelectedSymbol => _selectedSymbol;
        public event Action<string?>? SymbolSelected;
        public void SelectSymbol(string? symbol)
        {
            if (_selectedSymbol == symbol)
                return;
            _selectedSymbol = symbol;
            SymbolSelected?.Invoke(symbol);
        }
            
    }
}
