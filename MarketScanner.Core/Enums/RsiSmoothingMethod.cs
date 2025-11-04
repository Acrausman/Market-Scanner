using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketScanner.Core.Enums
{
    /// <summary>
    /// Smoothing method used when computing the Relative Strength Index (RSI).
    /// </summary>
    /// <remarks>
    /// <para><see cref="Wilder"/> is the classic RSI from J. Welles Wilder (a.k.a. RMA).</para>
    /// <para><see cref="Ema"/> uses EMA-style smoothing.</para>
    /// <para><see cref="Simple"/> uses arithmetic means over the lookback window.</para>
    /// </remarks>
    public enum RsiSmoothingMethod
    {
        /// <summary>
        /// Wilder's original smoothing (RMA): avg = (prevAvg * (N - 1) + value) / N.
        /// </summary>
        Wilder,

        /// <summary>
        /// Exponential (EMA) smoothing: avg = α * value + (1 - α) * prevAvg, where α = 2 / (N + 1).
        /// </summary>
        Ema,

        /// <summary>
        /// Simple arithmetic mean of the last N gains and losses.
        /// </summary>
        Simple
    }
}
