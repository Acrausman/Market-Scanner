using MarketScanner.Core.Abstractions;
using MarketScanner.Core.Classification;
using MarketScanner.Core.Models;
using MarketScanner.Data.Services.Alerts;
using System;

namespace MarketScanner.Data.Services.Analysis
{
    public class AlertDispatchService : IAlertDispatchService
    {
        private readonly IAlertManager _alertManager;
        public event Action<EquityScanResult>? ClassificationArrived;

        public AlertDispatchService(IAlertManager alertManager)
        {
            _alertManager = alertManager;
        }
        public void Dispatch(EquityScanResult result)
        {
            if(result.Tags.Contains("Overbought"))
            {
                _alertManager.Enqueue(result.Symbol, "overbought", result.RSI);
                ClassificationArrived?.Invoke(result);
                return;
            }
            if(result.Tags.Contains("Oversold"))
            {
                _alertManager.Enqueue(result.Symbol, "oversold", result.RSI);
                ClassificationArrived?.Invoke(result);
                return;
            }
        }
    }
}
