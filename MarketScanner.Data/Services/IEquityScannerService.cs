using MarketScanner.Data.Models;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace MarketScanner.Data.Services
{
    public interface IEquityScannerService
    {
        ObservableCollection<string> OverboughtSymbols { get; }
        ObservableCollection<string> OversoldSymbols { get; }

        Task ScanAllAsync(IProgress<int>? progress, CancellationToken token);
        Task<EquityScanResult> ScanSingleSymbol(string symbol);
        void ClearCache();
    }
}
