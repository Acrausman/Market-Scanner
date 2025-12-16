using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace MarketScanner.Core.Abstractions;

public interface IAlertManager
{
    /// <summary>
    /// Gets the collection of symbols that have triggered overbought conditions.
    /// </summary>
    ObservableCollection<string> OverboughtSymbols { get; }

    /// <summary>
    /// Gets the collection of symbols that have triggered oversold conditions.
    /// </summary>
    ObservableCollection<string> OversoldSymbols { get; }

    /// <summary>
    /// Gets the current count of overbought symbols.
    /// </summary>
    int OverboughtCount { get; }

    /// <summary>
    /// Gets the current count of oversold symbols.
    /// </summary>
    int OversoldCount { get; }

    /// <summary>
    /// Registers the sink that receives formatted alert messages.
    /// </summary>
    /// <param name="sink">The sink implementation to receive messages.</param>
    void SetSink(IAlertSink? sink);

    /// <summary>
    /// Queues an alert for the specified symbol and trigger.
    /// </summary>
    /// <param name="symbol">The ticker symbol that generated the alert.</param>
    /// <param name="triggerName">The name of the trigger that fired.</param>
    /// <param name="value">The indicator value that caused the alert.</param>
    void Enqueue(string symbol, string triggerName, double value);

    /// <summary>
    /// Flushes pending alerts and updates observable collections.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    Task FlushAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Clears pending alerts and tracked symbol collections.
    /// </summary>
    Task ResetAsync();
}
