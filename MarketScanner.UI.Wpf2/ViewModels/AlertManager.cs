using MarketScanner.Data.Models;
using MarketScanner.Data.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MarketScanner.UI.Wpf.Managers
{
    public class AlertManager
    {
        private readonly AlertService _alertService;
        private readonly EmailService _emailService;

        // 🟢 Keep a list of active alerts
        public List<Alert> Alerts { get; } = new();

        public AlertManager(AlertService alertService, EmailService emailService)
        {
            _alertService = alertService;
            _emailService = emailService;
        }

        // 🟢 Called by MarketDataEngine or MainViewModel for each new EquityScanResult
        public void ProcessScanResult(EquityScanResult result)
        {
            foreach (var alert in Alerts)
            {
                if (alert.Symbol != result.Symbol)
                    continue;

                if (_alertService.ShouldTrigger(alert, result))
                {
                    HandleAlert(alert, result);
                }
            }
        }

        private void HandleAlert(Alert alert, EquityScanResult result)
        {
            if (alert.isTriggered && alert.LastTriggered.HasValue &&
                (DateTime.Now - alert.LastTriggered.Value).TotalMinutes < 10)
                return; // avoid spam

            alert.isTriggered = true;
            alert.LastTriggered = DateTime.Now;

            string subject = $"Market Alert: {alert.Symbol}";
            string message = alert.Message ?? GenerateAlertMessage(alert, result);

            Debug.WriteLine($"[AlertManager] Triggering {alert.Type} for {alert.Symbol}");

            if (alert.NotifyEmail)
            {
                // send to your preferred email or configured address
                _emailService.SendEmail("your_email@gmail.com", "recipient@example.com", subject, message);
            }

            // future SMS support would go here
        }

        private string GenerateAlertMessage(Alert alert, EquityScanResult result)
        {
            return alert.Type switch
            {
                AlertType.RsiOverbought => $"{alert.Symbol} RSI is overbought ({result.RSI:F2}).",
                AlertType.RsiOversold => $"{alert.Symbol} RSI is oversold ({result.RSI:F2}).",
                AlertType.PriceAbove => $"{alert.Symbol} price rose above {alert.PriceAbove:F2} (current: {result.Price:F2}).",
                AlertType.PriceBelow => $"{alert.Symbol} price fell below {alert.PriceBelow:F2} (current: {result.Price:F2}).",
                _ => $"{alert.Symbol} triggered an alert."
            };
        }
    }
}
