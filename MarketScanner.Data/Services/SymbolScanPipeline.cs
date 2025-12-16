using MarketScanner.Core.Abstractions;
using MarketScanner.Core.Classification;
using MarketScanner.Core.Configuration;
using MarketScanner.Core.Enums;
using MarketScanner.Core.Models;
using MarketScanner.Data.Diagnostics;
using MarketScanner.Data.Providers;
using MarketScanner.Data.Services.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketScanner.Data.Services
{
    public class SymbolScanPipeline: ISymbolScanPipeline
    {
        private readonly HistoricalPriceCache _priceCache;
        private readonly IMetadataService _metadataService;
        private readonly IIndicatorService _indicatorService;
        private readonly IClassificationEngine _classificationEngine;
        private readonly IMarketDataProvider _provider;
        private readonly AppSettings _settings;

        //Possibly move these variables to the AppSettings
        private const int MinimumCloseCount = 150;
        private const int IndicatorWindow = 120;
        private const int IndicatorPeriod = 14;

        public SymbolScanPipeline(
            HistoricalPriceCache priceCache,
            IMetadataService metadataService,
            IIndicatorService indicatorService,
            IClassificationEngine classificationEngine,
            IMarketDataProvider provider,
            AppSettings settings)
        {
            _priceCache = priceCache;
            _metadataService = metadataService;
            _indicatorService = indicatorService;
            _classificationEngine = classificationEngine;
            _provider = provider;
            _settings = settings;
            
        }

        public async Task<EquityScanResult> ScanAsync(
            TickerInfo info,
            CancellationToken cancellationToken)
        {
            string symbol = info.Symbol;
            var closes = await _priceCache.GetClosingPricesAsync(symbol, MinimumCloseCount, cancellationToken).ConfigureAwait(false);
            if (closes == null || closes.Count < IndicatorPeriod)
            {
                //_logger.Log(LogSeverity.Warning, $"[Scanner] Skipping {symbol} due to missing data.");
                return CreateEmptyResult(symbol);
            }

            var trimmed = closes.Skip(Math.Max(0, closes.Count - IndicatorWindow)).ToList();
            if (trimmed.Count < IndicatorPeriod)
            {
                //_logger.Log(LogSeverity.Warning, $"[Scanner] Skipping {symbol} due to missing data.");
                return CreateEmptyResult(symbol);
            }

            info = await _metadataService
                .EnsureMetadataAsync(info, cancellationToken)
                .ConfigureAwait(false);

            var rsiMethod = _settings?.RsiMethod ?? RsiSmoothingMethod.Simple;
            var indicators = _indicatorService.CalculateIndicators(trimmed, IndicatorPeriod, rsiMethod);
            var (price, volume) = await _provider.GetQuoteAsync(symbol, cancellationToken)
                .ConfigureAwait(false);
            var result = new EquityScanResult
            {
                Symbol = symbol,
                Price = double.IsNaN(price) ? trimmed.LastOrDefault() : price,
                Volume = volume,
                RSI = indicators.RSI,
                SMA = indicators.SMA,
                Upper = indicators.UpperBand,
                Lower = indicators.LowerBand,
                TimeStamp = DateTime.UtcNow,
                MetaData = info
            };

            _classificationEngine.Classify(result);
            return result;
        }

        private static EquityScanResult CreateEmptyResult(string symbol)
        {
            return new EquityScanResult
            {
                Symbol = symbol,
                Price = double.NaN,
                Volume = double.NaN,
                RSI = double.NaN,
                SMA = double.NaN,
                Upper = double.NaN,
                Lower = double.NaN,
                TimeStamp = DateTime.UtcNow
            };
        }
    }
}
