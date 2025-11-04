namespace MarketScanner.Core.Models;

/// <summary>
/// Describes a single rule that evaluates a market condition. Populated by UI or configuration layers for consumption by analytics components.
/// </summary>
public record TriggerSettings
{
    /// <summary>
    /// Gets the human-readable name of the trigger supplied by configurators.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the trigger should be active. Outer layers toggle this flag to enable or disable logic.
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Gets settings for a relative strength index condition when applicable. Null indicates the trigger is of another type.
    /// </summary>
    public RsiOptions? Rsi { get; init; }
}
