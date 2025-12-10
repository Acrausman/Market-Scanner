using MarketScanner.Core.Configuration;
using MarketScanner.Core.Enums;
using MarketScanner.Core.Filtering;
using MarketScanner.Core.Metadata;
using MarketScanner.Data.Diagnostics;
using MarketScanner.Data.Providers;
using MarketScanner.Data.Services;
using MarketScanner.Data.Services.Analysis;
using MarketScanner.UI.Wpf.Services;
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

namespace MarketScanner.UI.Wpf.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ISymbolSelectionService _symbolSelectionService;
        private readonly EquityScannerService _scannerService;
        private readonly ScannerViewModel _scannerViewModel;
        private readonly TickerMetadataCache _metadataCache;
        private readonly ChartViewModel _chartViewModel;
        private readonly FilterPanelViewModel _filterPanelViewModel;
        public FilterPanelViewModel FilterPanel => _filterPanelViewModel;
        private readonly AlertPanelViewModel _alertPanelViewModel;
        public AlertPanelViewModel AlertPanel => _alertPanelViewModel;
        private readonly AlertCoordinatorService _alertCoordinatorService;
        private readonly FilterService _filterService;
        private readonly FilterCoordinatorService _filterCoordinator;
        private readonly IChartCoordinator _chartCoordinator;
        private readonly EmailService? _emailService;
        private readonly System.Timers.Timer _alertTimer;
        private readonly AlertManager _alertManager;
        private readonly UiNotifier _uiNotifier;
        private readonly IProgress<int> _scanProgress;

        private readonly IScanResultRouter _scanResultRouter;
        public IUiNotifier UiNotifier { get; }
        public ObservableCollection<string> AvailableSectors { get; }
            = new ObservableCollection<string>();
        public ObservableCollection<string> AvailableCountries { get; }
            = new ObservableCollection<string>();
        private readonly List<double> _intervalOptions = new() { 1, 5, 15, 30, 60}; //Minutes
        private int _selectedInterval = 15;
        private RsiSmoothingMethod _selectedRsiMethod;
        private readonly AppSettings _appSettings;
        private readonly Dispatcher _dispatcher;
        private readonly DispatcherTimer _digestTimer;
        private double _alertIntervalMinutes = 30;

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

        // cancellation tokens for long-running ops
        private CancellationTokenSource? _scanCts;

        // backing fields for bindable props
        private string _consoleText = string.Empty;
        private string _statusText = "Idle";
        private string? _selectedSymbol;
        private bool _isScanning;
        // persisted / options fields
        private string _notificationEmail = string.Empty;
        private RsiSmoothingMethod _rsiMethod;
        private string _selectedTimespan = "3M";
        public IEnumerable<double> IntervalOptions => _intervalOptions;
        public ICommand SendDigestNow { get; }
        public ICommand PauseScanCommand => _pauseScanCommand;
        public ICommand ResumeScanCommand => _resumeScanCommand;

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
            FilterPanelViewModel filterPanelViewModel,
            AlertPanelViewModel alertPanelViewModel,
            EmailService emailService,
            FilterService filterService,
            Dispatcher dispatcher,
            AlertManager alertManager,
            AppSettings settings,
            UiNotifier uiNotifier,
            TickerMetadataCache metadataCache,
            EquityScannerService scannerService)
        {
            _appSettings = settings;
            _dispatcher = dispatcher;
            _filterService = filterService;
            _filterCoordinator = new FilterCoordinatorService(_filterService, _appSettings);
            _scannerService = scannerService;
            _scannerViewModel = scannerViewModel;
            _chartViewModel = chartViewModel;
            _alertPanelViewModel = alertPanelViewModel;
            _alertCoordinatorService = new AlertCoordinatorService(alertPanelViewModel, dispatcher);
            _chartCoordinator = new ChartCoordinator(_chartViewModel, _dispatcher);
            _scannerService.ScanResultClassified += async result =>
            {
                await _chartCoordinator.OnScanResult(result);
            };
            _filterPanelViewModel = filterPanelViewModel;
            _emailService = emailService;
            _alertManager = alertManager;
            _uiNotifier = uiNotifier;
            _metadataCache = metadataCache;
            _scanResultRouter = new ScanResultRouter(_alertPanelViewModel, _dispatcher);
            _scannerService.ScanResultClassified += async result =>
            {
                await _chartCoordinator.OnScanResult(result);
                _scanResultRouter.HandleResult(result);
            };
            _symbolSelectionService = new SymbolSelectionService(scannerService, _chartViewModel);
      
            _scanProgress = new Progress<int>(value =>
            {
                _scannerViewModel.ProgressValue = value;
            });
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
            LoadFilterChoices();

            // Commands for options panel
            SaveEmailCommand = new RelayCommand(_ => SaveEmail());
            TestEmailCommand = new RelayCommand(_ => TestEmail());
            SendDigestNow = new RelayCommand(_ => _alertManager.SendPendingDigest(NotificationEmail));
            _filterPanelViewModel.FiltersApplied += OnFiltersApplied;
            _filterPanelViewModel.FiltersCleared += OnFiltersCleared;

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

            _ = Task.Run(async () =>
            {
                var updater = new UpdateService();
                await updater.CheckForUpdatesAsync();
            });
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
        private async void OnFiltersApplied()
        {
            _filterCoordinator.ApplyFilters(_filterPanelViewModel);
            ((App)App.Current).Notifier.Show("Filters applied!");
            if (IsScanning)
                await RestartScanAsync();
        }
        private async void OnFiltersCleared()
        {
            _filterCoordinator.ClearFilters(_filterPanelViewModel);
            ((App)App.Current).Notifier.Show("Filters cleared!");
            if (IsScanning)
                await RestartScanAsync();
        }
        public string? SelectedSymbol
        {
            get => _selectedSymbol;
            set
            {
                if (_selectedSymbol == value)
                    return;

                _selectedSymbol = value;
                OnPropertyChanged();

                _ = _chartCoordinator.OnSymbolSelected(value);
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
        private bool _isRestarting;
        private async Task RestartScanAsync()
        {
            if (_scannerService == null)
                return;
            if (_isRestarting)
                return;
            _isRestarting = true;
            try
            {
                StatusText = "Restarting scan with new settings...";

                _alertPanelViewModel.ClearAlertsCommand.Execute(null);
                ResetProgressUI();

                await _scannerService.RestartAsync(_scanProgress).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Error($"UI Restart scan failed: {ex}");
                StatusText = "Error restarting scan.";
            }
            finally
            {
                _isRestarting = false;
            }
        }
            
        private void ResetProgressUI()
        {
            _scannerViewModel.ProgressValue = 0;
        }

        // -------- Setting persistence --------

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
