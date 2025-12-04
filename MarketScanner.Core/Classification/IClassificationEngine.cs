using MarketScanner.Core.Models;
namespace MarketScanner.Core.Classification
{
    public interface IClassificationEngine
    {
        void Classify(EquityScanResult result);
    }
}
