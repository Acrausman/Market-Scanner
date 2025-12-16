using MarketScanner.Core.Models;
using System;
namespace MarketScanner.Core.Classification
{
    public interface IAlertDispatchService
    {
        void Dispatch(EquityScanResult result);
        event Action<EquityScanResult> ClassificationArrived;
    }
}
