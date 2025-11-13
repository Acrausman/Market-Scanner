using MarketScanner.Core.Models;
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
        Task<EquityScanResult> ScanSingleSymbol(TickerInfo info);
        Task<EquityScanResult> ScanSingleSymbol(TickerInfo info, CancellationToken cancellationToken);
        void ClearCache();
    }
}
