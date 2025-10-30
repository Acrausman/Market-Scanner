using MarketScanner.Data.Diagnostics;
using MarketScanner.Data.Models;
using MarketScanner.Data.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MarketScanner.UI.Wpf.Services
{
    public class AlertManager : IAlertSink
    {
        private readonly AlertService _alertService;
        private readonly EmailService _emailService;

        private readonly List<string> _pendingMessages = new();
        private readonly object _lock = new();
        private DateTime _lastDigestSent = DateTime.MinValue;

        public List<Alert> Alerts { get; } = new();

        public AlertManager(AlertService alertService, EmailService emailService)
        {
            _alertService = alertService;
            _emailService = emailService;
        }
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

        public void AddAlert(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;
            lock(_lock)
            {
                _pendingMessages.Add($"[{DateTime.Now:HH:mm}] {message}");
            }

            Logger.WriteLine($"[AlertManager] Queued alert: {message}");
            Logger.WriteLine($"[AlertManager] Total alerts are now {_pendingMessages.Count}");
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
                lock(_lock)
                {
                    _pendingMessages.Add($"{alert.Symbol}: {message}");
                }
            }
        }

        public void SendPendingDigest(string recipientEmail)
        {
            Logger.WriteLine($"Pending alerts: {_pendingMessages.Count}");

            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                Logger.WriteLine("[AlertManager] Digest skipped — no recipient email configured.");
                return;
            }

            List<string> snapshot;
            lock (_lock)
            {
                if (_pendingMessages.Count == 0)
                {
                    Logger.WriteLine("[AlertManager] No pending alerts to send.");
                    return;
                }

                snapshot = new List<string>(_pendingMessages);
                _pendingMessages.Clear();
            }

            var subject = $"MarketScanner RSI Digest ({DateTime.Now:HH:mm})";
            var body = "Recent Alerts:\n\n" + string.Join("\n", snapshot);

            Logger.WriteLine($"[AlertManager] Sending digest to {recipientEmail} with {snapshot.Count} entries.");
            _emailService.SendEmail(recipientEmail, subject, body);
            _lastDigestSent = DateTime.Now;
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
