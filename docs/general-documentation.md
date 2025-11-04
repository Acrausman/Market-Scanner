## High-level execution path

1. A full scan begins when the user triggers StartScanCommand, which calls StartScanAsync on MainViewModel. That method creates a new cancellation token source, shows progress in the status text, and forwards the progress reporter and token to ScannerViewModel.StartScanAsync.  
2. ScannerViewModel links the UI token with an internal CTS, mirrors progress updates back onto the UI thread, and invokes IEquityScannerService.ScanAllAsync on the injected scanner service to perform the work.  
3. EquityScannerService.ScanAllAsync resets alert state, asks the market data provider for the complete ticker list, spins up a SemaphoreSlim-bounded fan-out of ProcessSymbolAsync tasks, and waits for them with Task.WhenAll. It also flushes alerts at the end and posts summary logging via the shared ILogger.  
4. Each ProcessSymbolAsync acquires the semaphore, calls ScanSymbolCoreAsync, caches the result, queues RSI-based alerts, batches periodic flushes, and reports progress using Interlocked counters before releasing the semaphore and honoring cancellation delays.  
5. ScanSymbolCoreAsync pulls cleaned closing prices from HistoricalPriceCache, computes RSI/SMA/Bollinger with the indicator helpers, fetches a fresh quote, and returns an EquityScanResult that drives alert thresholds and downstream UI updates.  
6. Alerts are buffered in AlertManager, which logs queue changes, forwards formatted messages to any sink, and, upon FlushAsync, updates the OverboughtSymbols and OversoldSymbols collections on the WPF dispatcher so bindings stay thread-safe.  
7. When the user selects a symbol, MainViewModel.LoadSelectedSymbolAsync asks ChartViewModel to reload that ticker. The chart VM fetches historical bars from the provider, rebuilds SMA/Bollinger/RSI series, and pushes them through an IChartService onto the OxyPlot models for visualization.

### Layer interactions

* UI layer: MainViewModel wires up a Polygon provider, scanner VM, chart VM, and alert manager, exposes commands and status text, and relays symbol selections to the chart logic. ScannerViewModel and ChartViewModel expose observable collections/plot models for binding, and ScannerViewModel simply re-surfaces the alert collections that the service owns.  
* Service layer: EquityScannerService orchestrates scanning, using HistoricalPriceCache to reuse cleaned price history, IDataCleaner to normalize bars, and IAlertManager to manage alert sinks. Its constructors support dependency injection and wire default helpers when only a provider/alert sink are supplied.  
* Data & diagnostics layer: HistoricalPriceCache stores close series in a ConcurrentDictionary, only refetching when the cached data is stale; each fetch routes through IDataCleaner to remove duplicates and apply split adjustments before returning the tail window. DataCleaner in turn consults the provider for adjustments, removes duplicate timestamps, applies factors, and logs adjustments with the shared ILogger.  
* Provider layer: PolygonMarketDataProvider encapsulates quote, history, and split endpoints, delegating to PolygonBarDownloader for bar retrieval (including open/close reconciliation) and to PolygonCorporateActionService for split/dividend adjustments. It also paginates the Polygon ticker reference API when asked for the universe.  
* Indicator layer: Stateless helpers compute single values and full series for RSI, SMA, and Bollinger bands; the scanner uses single-value methods, while the chart VM uses series outputs for plotting.

### Data flow (symbol ➜ bars ➜ indicators ➜ alerts)

1. EquityScannerService requests ticker symbols from IMarketDataProvider.  
2. For each symbol, HistoricalPriceCache fetches raw bars via the provider, cleans them with DataCleaner, and emits closing prices (and caches the list).  
3. Indicator calculators produce the RSI/SMA/Bollinger metrics from the trimmed closing-price window.  
4. The provider supplies the most recent quote/volume to finalize the EquityScanResult.  
5. QueueAlerts inspects RSI for overbought/oversold thresholds and enqueues alerts with AlertManager, which updates observable collections during flushes for the UI to bind to.

### Control flow & orchestration details

* The scanner limits parallelism with SemaphoreSlim, increments processed counts atomically, and periodically flushes alerts and progress when batch sizes are reached.  
* At the end of a run, it forces a final flush and posts completion logs, ensuring alert sinks and UI collections are caught up.  
* AlertManager forwards formatted alert strings immediately to any sink (e.g., WPF list) while still batching the collection updates on the dispatcher to keep UI threading compliant.  
* Charts are refreshed independently of the scanning loop: selecting a ticker prompts ChartViewModel to pull historical bars directly from the provider, compute indicator series, and update IChartService plots on the UI thread.

### Concurrency, cancellation, and progress

* UI cancellation is propagated through linked token sources; ProcessSymbolAsync and cache/cleaner methods observe the token and throw when cancelled, leading to graceful cancellation logs.  
* Task.WhenAll drives the fan-out completion, and Task.Delay in the finally block prevents hammering the provider when throttled or cancelled.  
* Progress is reported via IProgress\<int\> passed from the UI. Batched updates call ReportProgress, which computes a percentage and emits increases only, while ScannerViewModel mirrors those updates back onto its ProgressValue property.  
* Logging is thread-safe through Logger’s locked Write method, and helper methods (Info/Warn/Error/Debug) preserve existing format strings used across services and providers.

