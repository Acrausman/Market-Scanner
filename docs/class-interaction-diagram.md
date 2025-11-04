# MarketScanner Class Interaction Overview

The diagram below summarizes how key classes in the `MarketScanner.UI.Wpf` layer orchestrate scanning and charting features by delegating to services housed under `MarketScanner.Data`.

```
MarketScanner.UI.Wpf
└─ MainViewModel
   ├─ owns ScannerViewModel
   │   └─ depends on IEquityScannerService (MarketScanner.Data.Services)
   │        └─ implemented by EquityScannerService
   │             ├─ uses IMarketDataProvider (e.g., PolygonMarketDataProvider)
   │             │    ├─ PolygonRestClient → PolygonBarDownloader → PolygonCorporateActionService
   │             │    └─ returns Bar / SplitAdjustment models
   │             ├─ uses HistoricalPriceCache (MarketScanner.Data.Services.Data)
   │             │    └─ pulls cleaned bars via IDataCleaner → DataCleaner
   │             ├─ calls indicator helpers (RsiCalculator, SmaCalculator, BollingerBandsCalculator)
   │             └─ queues alerts through IAlertManager → AlertManager → IAlertSink (UI AlertManager)
   ├─ owns ChartViewModel
   │   ├─ fetches bars via IMarketDataProvider
   │   ├─ builds series using indicator calculators
   │   └─ pushes data into IChartService for OxyPlot visuals
   └─ wires AlertManager (UI) as IAlertSink recipient for queued alerts

Data flow (per symbol)
1. UI requests scan ➜ EquityScannerService retrieves ticker list from provider.
2. Provider fetches raw bars/quotes ➜ HistoricalPriceCache caches cleaned data via DataCleaner.
3. Indicator helpers compute RSI/SMA/Bollinger ➜ EquityScannerService evaluates triggers.
4. Trigger hits enqueue alerts in AlertManager ➜ flushed to UI AlertManager for display/email.
5. ChartViewModel independently pulls bars and indicators for visualization through IChartService.
```

Key concurrency & reporting touchpoints:
- `ScannerViewModel` links UI cancellation tokens and forwards progress to `EquityScannerService`.
- `EquityScannerService` bounds parallel symbol processing with `SemaphoreSlim`, reports progress via `IProgress<int>`, and respects cancellation tokens throughout provider, cache, and alert operations.
- `AlertManager` (data layer) updates observable collections on the WPF dispatcher before the UI layer reads them.
