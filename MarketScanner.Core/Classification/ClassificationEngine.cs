using MarketScanner.Core.Classification;
using MarketScanner.Core.Models;

namespace MarketScanner.Core.Classification
{
    public class ClassificationEngine : IClassificationEngine
    {
        private readonly IReadOnlyList<IEquityClassifier> _classifiers;
        public ClassificationEngine(IEnumerable<IEquityClassifier> classifiers)
        {
            _classifiers = classifiers.ToList();
        }
        public void Classify(EquityScanResult result)
        {
            foreach (var classifier in _classifiers) 
                classifier.Classify(result);
        }
    }
}
