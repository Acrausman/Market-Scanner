namespace MarketScanner.Core.Abstractions;

/// <summary>
/// Defines the severity levels for logging messages produced by infrastructure implementations of <see cref="IAppLogger"/>.
/// </summary>
public enum LogSeverity
{
    /// <summary>
    /// Informational events that highlight the progress of the application.
    /// </summary>
    Information,

    /// <summary>
    /// Potentially harmful situations that warrant attention.
    /// </summary>
    Warning,

    /// <summary>
    /// Error events that may still allow the application to continue running.
    /// </summary>
    Error,

    /// <summary>
    /// Detailed diagnostic events intended for development or troubleshooting scenarios.
    /// </summary>
    Debug
}
