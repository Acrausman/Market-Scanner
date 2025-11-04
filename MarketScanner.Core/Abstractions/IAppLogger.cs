namespace MarketScanner.Core.Abstractions;

/// <summary>
/// Contract for logging that must be implemented by hosting layers such as UI or infrastructure projects.
/// </summary>
public interface IAppLogger
{
    /// <summary>
    /// Records an application message using the specified severity. Implemented by outer layers to direct logs to appropriate sinks.
    /// </summary>
    /// <param name="severity">Severity associated with the log entry.</param>
    /// <param name="message">Human-readable message to persist.</param>
    /// <param name="exception">Optional exception that supplies additional diagnostic context.</param>
    void Log(LogSeverity severity, string message, Exception? exception = null);
}
