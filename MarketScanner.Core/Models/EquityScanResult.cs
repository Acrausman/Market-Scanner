using System;
using System.Collections.Generic;

namespace MarketScanner.Core.Models;

/// <summary>
/// Represents the outcome of analysing a symbol during a scan. Data and UI layers
/// exchange this model without sharing implementation details.
/// </summary>
/// 
public enum CreeperType
{
    Uptrend,
    Downtrend,
    Accumulation
}
public record EquityScanResult
{
    /// <summary>
    /// Gets the symbol's country of origin
    /// </summary>
    public string Country { get; init; } = string.Empty;
    /// <summary>
    /// Gets the ticker symbol that was evaluated.
    /// </summary>
    /// 
    public string Sector { get; init; } = string.Empty;
    /// <summary>
    /// Gets the symbol's sector
    /// </summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>
    /// Gets the most recent trade price observed for the symbol.
    /// </summary>
    public double Price { get; init; } = double.NaN;

    /// <summary>
    /// Gets the current RSI value calculated for the configured lookback period.
    /// </summary>
    public double RSI { get; init; } = double.NaN;

    /// <summary>
    /// Gets the simple moving average computed for the symbol.
    /// </summary>
    public double SMA { get; init; } = double.NaN;

    /// <summary>
    /// Gets the upper Bollinger band derived from the observed closes.
    /// </summary>
    public double Upper { get; init; } = double.NaN;

    /// <summary>
    /// Gets the lower Bollinger band derived from the observed closes.
    /// </summary>
    public double Lower { get; init; } = double.NaN;

    /// <summary>
    /// Gets the latest traded volume reported by the market data provider.
    /// </summary>
    public double Volume { get; init; } = double.NaN;

    /// <summary>
    /// Gets the timestamp when the scan was produced.
    /// </summary>
    public DateTime TimeStamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the triggers that fired as part of the scan evaluation.
    /// </summary>
    /// 
    public TickerInfo MetaData { get; set; } = new TickerInfo();
    public List<string> Tags { get; } = new List<string>();
    public IReadOnlyList<TriggerHit> TriggerHits { get; init; } = Array.Empty<TriggerHit>();

    public bool IsOverbought { get; set; }
    public bool IsOversold { get; set; }
    public bool IsCreeper { get; set; }
    public double CreeperScore {  get; set; }
    public CreeperType? CreeperType { get; set; }
}
