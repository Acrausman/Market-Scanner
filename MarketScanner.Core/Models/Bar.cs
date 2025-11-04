using System;

namespace MarketScanner.Core.Models;

/// <summary>
/// Represents a single market data bar. Data providers populate the values and
/// analytics layers consume them to derive indicators.
/// </summary>
public record Bar
{
    /// <summary>
    /// Gets the UTC timestamp for the bar as supplied by the market data source.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the opening price observed during the interval.
    /// </summary>
    public double Open { get; init; }

    /// <summary>
    /// Gets the highest price observed during the interval.
    /// </summary>
    public double High { get; init; }

    /// <summary>
    /// Gets the lowest price observed during the interval.
    /// </summary>
    public double Low { get; init; }

    /// <summary>
    /// Gets the closing price for the interval.
    /// </summary>
    public double Close { get; set; }

    /// <summary>
    /// Gets the traded volume for the interval.
    /// </summary>
    public double Volume { get; set; }
}
