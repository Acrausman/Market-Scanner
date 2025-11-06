using MarketScanner.Core.Models;
namespace MarketScanner.Core.Filtering
{
    /// <summary>
    /// Defines search filters that the user can select to specify what they're looking for
    /// </summary>
    public interface IFilter
    {
        /// <summary>
        /// The filter name
        /// </summary>
        string Name { get; }
        /// <summary>
        /// Whether or not the filter applies
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        bool Matches(EquityScanResult result);
    }
}
