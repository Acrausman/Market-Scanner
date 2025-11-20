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

        bool Matches(EquityScanResult info);
  
    }
}
