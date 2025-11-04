namespace MarketScanner.Core.Models;

/// <summary>
/// Captures configuration values for an RSI-based trigger. Implementations outside the core library interpret these values.
/// </summary>
public record RsiOptions
{
    /// <summary>
    /// Gets the number of periods to use in RSI calculations.
    /// </summary>
    public int Period { get; init; } = 14;

    /// <summary>
    /// Gets the RSI threshold above which an asset is considered overbought.
    /// </summary>
    public decimal Overbought { get; init; } = 70m;

    /// <summary>
    /// Gets the RSI threshold below which an asset is considered oversold.
    /// </summary>
    public decimal Oversold { get; init; } = 30m;
}
