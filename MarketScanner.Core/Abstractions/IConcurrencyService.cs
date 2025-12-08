using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MarketScanner.Core.Abstractions
{
    public interface IConcurrencyService
    {
        Task RunForEachAsync<T>(
            IEnumerable<T> items,
            int maxConcurrency,
            Func<T, CancellationToken, Task> operation,
            CancellationToken cancellationToken);
    }
}
