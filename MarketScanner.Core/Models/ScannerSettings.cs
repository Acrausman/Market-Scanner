namespace MarketScanner.Core.Models;

/// <summary>
/// Immutable configuration snapshot shared across application layers. Constructed by configuration providers in data or UI projects.
/// </summary>
public record ScannerSettings
{
    /// <summary>
    /// Gets the collection of market symbols that the scanner should evaluate. Outer layers populate this list from user input or persisted configuration.
    /// </summary>
    public IReadOnlyList<string> Symbols { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the delay between successive scans. Host applications determine the appropriate cadence.
    /// </summary>
    public TimeSpan ScanInterval { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets trigger definitions that should be evaluated during a scan. Each definition is maintained by user-facing layers.
    /// </summary>
    public IReadOnlyList<TriggerSettings> Triggers { get; init; } = Array.Empty<TriggerSettings>();

    /// <summary>
    /// Gets the maximum number of concurrent symbol evaluations allowed. Infrastructure layers interpret this value when dispatching work.
    /// </summary>
    public int MaxConcurrentScans { get; init; } = 1;
}
