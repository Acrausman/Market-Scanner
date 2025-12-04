using MarketScanner.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketScanner.Data.Services
{
    public interface IMetadataService
    {
        Task PreloadAsync(IProgress<int>? progress, CancellationToken cancellation);

        Task<TickerInfo> EnsureMetadataAsync(TickerInfo info, CancellationToken cancellationToken);
    }
}
