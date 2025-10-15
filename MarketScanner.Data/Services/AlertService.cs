using System;
using System.Diagnostics;
using MarketScanner.Data.Models;

namespace MarketScanner.Data.Services
{
    public class AlertService
    {
        public bool ShouldTrigger(Alert alert, EquityScanResult result)
        {
            if (alert == null || result == null)
                return false;

            bool trigger = false;

            switch (alert.Type)
            {
                case AlertType.RsiOverbought:
                    if (alert.RsiAbove.HasValue && result.RSI >= alert.RsiAbove.Value)
                    {
                        trigger = true;
                        alert.Message = $"{result.Symbol} RSI is overbought ({result.RSI:F2} ≥ {alert.RsiAbove.Value})";
                    }
                    break;

                case AlertType.RsiOversold:
                    if (alert.RsiBelow.HasValue && result.RSI <= alert.RsiBelow.Value)
                    {
                        trigger = true;
                        alert.Message = $"{result.Symbol} RSI is oversold ({result.RSI:F2} ≤ {alert.RsiBelow.Value})";
                    }
                    break;

                case AlertType.PriceAbove:
                    if (alert.PriceAbove.HasValue && result.Price >= alert.PriceAbove.Value)
                    {
                        trigger = true;
                        alert.Message = $"{result.Symbol} price is above {alert.PriceAbove.Value:C} (current {result.Price:C})";
                    }
                    break;

                case AlertType.PriceBelow:
                    if (alert.PriceBelow.HasValue && result.Price <= alert.PriceBelow.Value)
                    {
                        trigger = true;
                        alert.Message = $"{result.Symbol} price is below {alert.PriceBelow.Value:C} (current {result.Price:C})";
                    }
                    break;

                default:
                    Debug.WriteLine($"[AlertService] Unknown alert type: {alert.Type}");
                    break;
            }

            if (trigger)
                Debug.WriteLine($"[AlertService] Triggered: {alert.Message}");

            return trigger;
        }
    }
}
