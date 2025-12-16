using MarketScanner.Core.Models;
using MarketScanner.Data.Services;

namespace MarketScanner.UI.Wpf.Services
{
    public interface IScanResultRouter
    {
        void HandleResult(EquityScanResult result);
    }
}
