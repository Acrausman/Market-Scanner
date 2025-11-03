using MarketScanner.Data.Services;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace MarketScanner.Data.Services.Alerts
{
    public interface IAlertManager
    {
        ObservableCollection<string> OverboughtSymbols { get; }
        ObservableCollection<string> OversoldSymbols { get; }
        int OverboughtCount { get; }
        int OversoldCount { get; }

        void SetSink(IAlertSink? sink);
        void Enqueue(string symbol, string triggerName, double value);
        Task FlushAsync(CancellationToken cancellationToken);
        Task ResetAsync();
    }
}
