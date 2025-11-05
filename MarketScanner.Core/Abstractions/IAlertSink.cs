namespace MarketScanner.Core.Abstractions;

/// <summary>
/// Represents a consumer of alert messages produced during market scans.
/// Implementations typically surface the notifications to users or logs.
/// </summary>
public interface IAlertSink
{
    /// <summary>
    /// Records a formatted alert message for later presentation.
    /// </summary>
    /// <param name="message">The formatted alert text to record.</param>
    void AddAlert(string message);
}
