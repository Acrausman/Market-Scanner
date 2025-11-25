using MarketScanner.Data.Diagnostics;
using MarketScanner.Data.Providers;
using MarketScanner.Data.Services;
using MarketScanner.Core.Configuration;
using MarketScanner.UI.Wpf.Services;
using MarketScanner.Core.Enums;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using MarketScanner.Core.Filtering;
using MarketScanner.Core.Models;
using MarketScanner.Core.Metadata;
using MarketScanner.Data.Providers.Finnhub;
using System.IO;

namespace MarketScanner.UI.Wpf.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IMarketDataProvider _provider;
        private readonly IFundamentalProvider _fundamentalProvider;
        private readonly EquityScannerService _scannerService;
        private readonly ScannerViewModel _scannerViewModel;
        private readonly EquityScannerService _equityScannerService;
        private readonly TickerMetadataCache _metadataCache;
        private readonly ChartViewModel _chartViewModel;
        private readonly EmailService? _emailService;
        private readonly System.Timers.Timer _alertTimer;
        private readonly AlertManager _alertManager;
        private readonly UiNotifier _uiNotifier;
        public IUiNotifier UiNotifier { get; }
        public double _minPrice;
        public double _maxPrice;
        //public string _selectedCountryFilter {  get; set; }
        //public string _selectedSectorFilter { get; set; }
        public ObservableCollection<string> AvailableSectors { get; }
            = new ObservableCollection<string>();
        public ObservableCollection<string> SelectedSectors { get; }
            = new ObservableCollection<string>();
        public ObservableCollection<string> AvailableCountries { get; }
            = new ObservableCollection<string>();
        public ObservableCollection<string> SelectedCountries { get; }
            = new ObservableCollection<string>();
        private readonly List<double> _intervalOptions = new() { 1, 5, 15, 30, 60}; //Minutes
        private int _selectedInterval = 15;
        private RsiSmoothingMethod _selectedRsiMethod;
        private readonly AppSettings _appSettings;
        private readonly Dispatcher _dispatcher;
        private readonly DispatcherTimer _digestTimer;
        private double _alertIntervalMinutes = 30;
        public ObservableCollection<TickerInfo> FilteredSymbols => _scannerService.FilteredSymbols;


        private bool enableEmail = false;
        public double AlertIntervalMinutes
        {
            get => _alertIntervalMinutes;
            set
            {
                if(Math.Abs(_alertIntervalMinutes - value) > 0.01)
                {
                    _alertIntervalMinutes = value;
                    OnPropertyChanged(nameof(AlertIntervalMinutes));
                    UpdateAlertTimerInterval();
                }
            }
        }

        // running console text buffer for in-app "console"
        private readonly StringBuilder _consoleBuilder = new();

        // commands
        private readonly RelayCommand _startScanCommand;
        private readonly RelayCommand _stopScanCommand;
        private readonly RelayCommand _pauseScanCommand;
        private readonly RelayCommand _resumeScanCommand;
        private readonly RelayCommand _applyFiltersCommand;


        // cancellation tokens for long-running ops
        private CancellationTokenSource? _scanCts;
        private CancellationTokenSource? _symbolCts;

        // backing fields for bindable props
        private string _consoleText = string.Empty;
        private string _statusText = "Idle";
        private string? _selectedSymbol;
        private bool _isScanning;
        // persisted / options fields
        private string apiKey = "YISIR_KLqJAdX7U6ix6Pjkyx70C_QgpI";
        private string FinnApiKey = "d44drfhr01qt371uia8gd44drfhr01qt371uia90";
        private string _notificationEmail = string.Empty;
        private RsiSmoothingMethod _rsiMethod;
        private string _selectedTimespan = "3M";
        public IEnumerable<double> IntervalOptions => _intervalOptions;
        public ICommand SendDigestNow { get; }
        public ICommand PauseScanCommand => _pauseScanCommand;
        public ICommand ResumeScanCommand => _resumeScanCommand;
        public ICommand ApplyFiltersCommand => _applyFiltersCommand;
        public IEnumerable<RsiSmoothingMethod> RsiMethods { get; }
            = new ObservableCollection<RsiSmoothingMethod>(
                Enum.GetValues(typeof(RsiSmoothingMethod)).Cast<RsiSmoothingMethod>());
        public int SelectedInterval
        {
            get => _selectedInterval;
            set
            {
                if (_selectedInterval != value)
                {
                    _selectedInterval = value;
                    OnPropertyChanged(nameof(SelectedInterval));

                    // Restart timer with new interval
                    _alertTimer.Interval = TimeSpan.FromMinutes(_selectedInterval).TotalMilliseconds;
                    _alertTimer.Start();

                    _appSettings.AlertIntervalMinutes = _selectedInterval;
                    _appSettings.Save();

                    Logger.Info($"[Options] Alert interval updated: {_selectedInterval} minutes");
                }
            }
        }


        public MainViewModel(
            ScannerViewModel scannerViewModel,
            ChartViewModel chartViewModel,
            EmailService emailService,
            Dispatcher dispatcher,
            AlertManager alertManager,
            AppSettings settings,
            UiNotifier uiNotifier,
            TickerMetadataCache metadataCache,
            EquityScannerService scannerService)
        {
            _scannerService = scannerService;
            _scannerViewModel = scannerViewModel;
            _chartViewModel = chartViewModel;
            _dispatcher = dispatcher;
            _emailService = emailService;
            _alertManager = alertManager;
            _appSettings = settings;
            _uiNotifier = uiNotifier;
            _metadataCache = metadataCache;
            _scannerService.ScanResultClassified += OnScanResultClassified;
            //if (_scannerService != null) _scannerService.AddFilter(new PriceFilter(5, 30));

            // Commands that show up in XAML
            _startScanCommand = new RelayCommand(async _ => await StartScanAsync(), _ => !IsScanning);
            _stopScanCommand = new RelayCommand(_ => StopScan(), _ => IsScanning);
            _pauseScanCommand = new RelayCommand(_ => PauseScan(), _ => IsScanning);
            _resumeScanCommand = new RelayCommand(_ => ResumeScan(), _ => IsScanning);

            // Load persisted settings
            _appSettings = settings;
            _notificationEmail = _appSettings.NotificationEmail ?? string.Empty;
            _selectedTimespan = string.IsNullOrWhiteSpace(_appSettings.SelectedTimespan)
                ? "3M"
                : _appSettings.SelectedTimespan;
            _selectedRsiMethod = _appSettings.RsiMethod;
            _selectedInterval = _appSettings.AlertIntervalMinutes > 0
                ? _appSettings.AlertIntervalMinutes
                : 15;
            _minPrice = _appSettings.FilterMinPrice;
            _maxPrice = _appSettings.FilterMaxPrice;
            foreach (var s in _appSettings.FilterCountries)
                SelectedCountries.Add(s);
            foreach(var s in _appSettings.FilterSectors) 
                SelectedSectors.Add(s);
            LoadFilterChoices();

            // Commands for options panel
            SaveEmailCommand = new RelayCommand(_ => SaveEmail());
            TestEmailCommand = new RelayCommand(_ => TestEmail());
            SendDigestNow = new RelayCommand(_ => _alertManager.SendPendingDigest(NotificationEmail));
            _applyFiltersCommand = new RelayCommand(_ => RebuildFiltersAndRestart());

            // push initial persisted values through their setters
            NotificationEmail = _notificationEmail;
            SelectedTimespan = _selectedTimespan;
            _digestTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(_appSettings.AlertIntervalMinutes)
            };
            _digestTimer.Tick += (s, e) =>
            {
                Logger.Info($"[Timer] Triggering digest at {DateTime.Now:T}");
                if(enableEmail)_alertManager.SendPendingDigest(NotificationEmail ?? string.Empty);
            };

            _digestTimer.Start();

        }

        // -------- Public bindable collections / exposed viewmodels --------

        public ObservableCollection<string> TimeSpanOptions { get; } =
            new ObservableCollection<string> { "1M", "3M", "6M", "1Y", "YTD", "Max" };

        public ScannerViewModel Scanner => _scannerViewModel;
        public ChartViewModel Chart => _chartViewModel;

        // If you want to bind to EmailService in XAML, expose it as nullable
        public EmailService? EmailService => _emailService;

        // -------- Console / Status UI --------

        public string ConsoleText
        {
            get => _consoleText;
            private set => SetProperty(ref _consoleText, value);
        }

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        // -------- Symbol selection / chart sync --------

        public string? SelectedSymbol
        {
            get => _selectedSymbol;
            set
            {
                if (SetProperty(ref _selectedSymbol, value))
                {
                    _ = LoadSelectedSymbolAsync(value);
                }
            }
        }

        // -------- Scan control / commands --------

        public ICommand StartScanCommand => _startScanCommand;
        public ICommand StopScanCommand => _stopScanCommand;

        private bool IsScanning
        {
            get => _isScanning;
            set
            {
                if (SetProperty(ref _isScanning, value, nameof(IsScanning)))
                {
                    // update CanExecute on the commands after state change
                    _dispatcher.InvokeAsync(() =>
                    {
                        _startScanCommand.RaiseCanExecuteChanged();
                        _stopScanCommand.RaiseCanExecuteChanged();
                    });
                }
            }
        }

        private void UpdateAlertTimerInterval()
        {
            if (_digestTimer == null)
            {
                Logger.Warn("[Timer] Attempted to change interval, but timer not initialized.");
                return;
            }

            _digestTimer.Stop();
            _digestTimer.Interval = TimeSpan.FromMinutes(_alertIntervalMinutes);
            _digestTimer.Start();

            Logger.Info($"[Timer] Digest interval updated to {_alertIntervalMinutes} minutes.");
        }

        private async Task StartScanAsync()
        {
            if (IsScanning)
                return;
            
            _scanCts = new CancellationTokenSource();
            IsScanning = true;
            StatusText = "Scanning...";
            Log("Starting equity scan...");

            var progress = new Progress<int>(value =>
            {
                _dispatcher.InvokeAsync(() => StatusText = $"Scanning... {value}%");
            });

            try
            {
                await _scannerViewModel.StartScanAsync(progress, _scanCts.Token).ConfigureAwait(false);
                await _dispatcher.InvokeAsync(() => StatusText = "Scan complete");
            }
            catch (OperationCanceledException)
            {
                await _dispatcher.InvokeAsync(() => StatusText = "Scan cancelled");
                Log("Scan cancelled by user.");
            }
            catch (Exception ex)
            {
                await _dispatcher.InvokeAsync(() => StatusText = "Scan failed");
                Log($"Scan failed: {ex.Message}");
            }
            finally
            {
                _scanCts?.Dispose();
                _scanCts = null;
                IsScanning = false;
            }
        }
        
        private void StopScan()
        {
            if (!IsScanning)
                return;
            _scanCts?.Cancel();
        }

        private void PauseScan()
        {
            _scannerService.Pause();
            StatusText = "Scan paused";
            Log("Scan paused");
        }

        private void ResumeScan()
        {
            _scannerService.Resume();
            StatusText = "Scan resumed";
            Log("Scan resumed");
        }

        private async Task RestartScanAsync()
        {
            try
            {
                if (_scannerService.IsScanning)
                    await _scannerService.StopAsync();

                _scannerViewModel.OverboughtSymbols.Clear();
                _scannerViewModel.OversoldSymbols.Clear();
                _scannerService.ClearCache();
                await _scannerService.StopAsync();
                await Task.Delay(250);
                await _scannerService.StartAsync();
            }
            catch (Exception ex)
            {
                Logger.Error($"[Scanner] Failed to restart scan: {ex.Message}");
            }
        }

        private void OnScanResultClassified(EquityScanResult result)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                if (result.RSI >= 70)
                    _scannerViewModel.OverboughtSymbols.Add(result.Symbol);
                else if (result.RSI <= 30)
                    _scannerViewModel.OversoldSymbols.Add(result.Symbol);
            });
        }

        private async Task RebuildFiltersAndRestart()
        {
            var filters = new List<IFilter>();

            if (_minPrice > 0 || _maxPrice < 99999)
                filters.Add(new PriceFilter(_minPrice, _maxPrice));
            if (SelectedCountries.Count > 0 && !SelectedCountries.Contains("Any"))
                filters.Add(new MultiCountryFilter(SelectedCountries));
            if (SelectedSectors.Count > 0 && !SelectedSectors.Contains("Any"))
                filters.Add(new MultiSectorFilter(SelectedSectors));

            _appSettings.FilterCountries = SelectedCountries.ToList();
            _appSettings.FilterSectors = SelectedSectors.ToList();
            _appSettings.Save();

            _scannerService.AddMultipleFilters(filters);
            Console.WriteLine("[DEBUG FILTER] Selected sectors: " +
                  string.Join(",", SelectedSectors));
            ((App)App.Current).Notifier.Show("Filters applied!");
            //await _uiNotifier.ShowStatusAsync("Filters applied!");
            //StartScanCommand.Execute(null);
        }

        // -------- Chart loading for selected symbol --------

        private async Task LoadSelectedSymbolAsync(string? symbol)
        {
            // cancel any in-flight symbol load
            var previous = _symbolCts;
            previous?.Cancel();
            previous?.Dispose();

            _symbolCts = new CancellationTokenSource();
            var currentCts = _symbolCts;
            var token = currentCts.Token;

            if (string.IsNullOrWhiteSpace(symbol))
            {
                StatusText = "Select a symbol to view details";
                _chartViewModel.Clear();
                return;
            }

            try
            {
                StatusText = $"Loading {symbol}...";
                await _chartViewModel.LoadChartForSymbol(symbol).ConfigureAwait(false);
                await _dispatcher.InvokeAsync(() => StatusText = $"Showing {symbol}");
            }
            catch (OperationCanceledException)
            {
                // selection changed, ignore
            }
            catch (Exception ex)
            {
                await _dispatcher.InvokeAsync(() => StatusText = $"Error loading {symbol}");
                Log($"Failed to load {symbol}: {ex.Message}");
            }
            finally
            {
                if (ReferenceEquals(_symbolCts, currentCts))
                {
                    _symbolCts.Dispose();
                    _symbolCts = null;
                }
            }
        }

        // -------- Setting persistence --------

        public double MinPrice
        {
            get => _minPrice;
            set
            {
                if(_minPrice != value)
                {
                    _minPrice = value;
                    OnPropertyChanged();

                    _appSettings.FilterMinPrice = _minPrice;
                    _appSettings.Save();

                }
            }
        }
        public double MaxPrice
        {
            get => _maxPrice;
            set
            {
                if (_maxPrice != value)
                {
                    _maxPrice = value;
                    OnPropertyChanged();

                    _appSettings.FilterMaxPrice = _maxPrice;
                    _appSettings.Save();

                }
            }
        }
        /*public string SelectedCountryFilter
        {
            get => _selectedCountryFilter;
            set
            {
                if (_selectedCountryFilter != value)
                {
                    _selectedCountryFilter = value ?? string.Empty;
                    OnPropertyChanged();

                    SelectedCountries.Clear();
                    if(!string.IsNullOrWhiteSpace(_selectedCountryFilter) &&
                        _selectedCountryFilter != "Any")
                    {
                        SelectedSectors.Add(_selectedCountryFilter);
                    }
                    _appSettings.Save();
                }
            }
        }
        
        public string SelectedSectorFilter
        {
            get => _selectedSectorFilter;
            set
            {
                if(_selectedSectorFilter != value)
                {
                    _selectedSectorFilter = value ?? string.Empty;
                    OnPropertyChanged();

                    SelectedSectors.Clear();
                    if (!string.IsNullOrWhiteSpace(_selectedSectorFilter) &&
                        _selectedSectorFilter != "Any")
                    {
                        SelectedSectors.Add(_selectedSectorFilter);
                    }

                    _appSettings.FilterSectors = SelectedSectors.ToList();
                    _appSettings.Save();
                }
            }
        
        }
        */
        private void LoadFilterChoices()
        {
            AvailableSectors.Clear();
            AvailableCountries.Clear();

            AvailableSectors.Add("Any");
            AvailableCountries.Add("Any");

            foreach (string s in _metadataCache.GetAllSectors())
                AvailableSectors.Add(s);
            foreach (string c in _metadataCache.GetAllCountries())
                AvailableCountries.Add(c);
            
        }
        public string NotificationEmail
        {
            get => _notificationEmail;
            set
            {
                if (_notificationEmail != value)
                {
                    _notificationEmail = value ?? string.Empty;
                    OnPropertyChanged(nameof(NotificationEmail));

                    _appSettings.NotificationEmail = _notificationEmail;
                    _appSettings.Save();

                    Logger.Info($"[Settings] NotificationEmail now '{_notificationEmail}'");
                }
            }
        }

        public RsiSmoothingMethod SelectedRsiMethod
        {
            get => _selectedRsiMethod;
            set
            {
                if(_selectedRsiMethod != value)
                {
                    _selectedRsiMethod = value;
                    OnPropertyChanged(nameof(_rsiMethod));

                    _appSettings.RsiMethod = _selectedRsiMethod;
                    _appSettings.Save();
                    Logger.Info($"[Settings] RSI Smoothing is now set to '{_selectedRsiMethod}'");

                    _ = RestartScanAsync();
                }

            }
        }

        public string SelectedTimespan
        {
            get => _selectedTimespan;
            set
            {
                if (_selectedTimespan != value && !string.IsNullOrWhiteSpace(value))
                {
                    _selectedTimespan = value;
                    OnPropertyChanged(nameof(SelectedTimespan));

                    _appSettings.SelectedTimespan = _selectedTimespan;
                    _appSettings.Save();

                }
            }
        }

        public void Dispose()
        {
            _alertTimer?.Stop();
            _alertTimer?.Dispose();
        }

        public ICommand SaveEmailCommand { get; }
        private void SaveEmail()
        {
            if (!string.IsNullOrWhiteSpace(NotificationEmail))
            {
                Logger.Info($"[Options] Email saved: {NotificationEmail}");
                // Additional persistence already handled in setter
            }
        }

        public ICommand TestEmailCommand { get; }
        private void TestEmail()
        {
            Logger.Info("[Email] Test email triggered.");

            if (string.IsNullOrWhiteSpace(NotificationEmail))
            {
                Logger.Warn("[Email] Cannot send test: no address configured.");
                return;
            }

            if (_emailService == null)
            {
                Logger.Warn("[Email] Cannot send test: no email service available.");
                return;
            }

            try
            {
                _emailService.SendEmail(
                    NotificationEmail,
                    "Test Email",
                    "This is a test email from MarketScanner."
                );

                Logger.Info($"[Email] Test message sent to {NotificationEmail}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[Email] Send failed: {ex.Message}");
            }
        }

        // -------- Console logging helper --------

        private void Log(string message)
        {
            var timestamped = $"[{DateTime.Now:HH:mm:ss}] {message}";
            Logger.Debug(timestamped);

            _dispatcher.InvokeAsync(() =>
            {
                if (_consoleBuilder.Length > 0)
                    _consoleBuilder.AppendLine();

                _consoleBuilder.Append(timestamped);
                ConsoleText = _consoleBuilder.ToString();
            });
        }

        // -------- INotifyPropertyChanged helpers --------

        private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
                return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
