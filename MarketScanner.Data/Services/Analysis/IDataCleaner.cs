using MarketScanner.Core.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MarketScanner.Data.Services.Analysis
{
    public interface IDataCleaner
    {
        Task<IReadOnlyList<Bar>> CleanAsync(string symbol, IReadOnlyList<Bar> bars, CancellationToken cancellationToken);
    }
}
