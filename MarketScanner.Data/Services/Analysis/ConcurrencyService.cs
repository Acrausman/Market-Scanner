using MarketScanner.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MarketScanner.Data.Services.Analysis
{
    public class ConcurrencyService : IConcurrencyService
    {
        public async Task RunForEachAsync<T>(
            IEnumerable<T> items,
            int maxConcurrency,
            Func<T, CancellationToken, Task> operation,
            CancellationToken cancellationToken)
        {
            using var limter = new SemaphoreSlim(maxConcurrency, maxConcurrency);

            var tasks = items.Select(async item =>
            {
                await limter.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await operation(item, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    limter.Release();
                }
            });
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }
}
