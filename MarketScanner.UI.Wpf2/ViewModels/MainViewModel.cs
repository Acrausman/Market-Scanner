using MarketScanner.Data.Diagnostics;
using MarketScanner.UI.Wpf.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        private readonly ScannerViewModel _scannerViewModel;
        private readonly ChartViewModel _chartViewModel;
        private readonly Dispatcher _dispatcher;
        private readonly StringBuilder _consoleBuilder = new();
        private readonly RelayCommand _startScanCommand;
        private readonly RelayCommand _stopScanCommand;
        private CancellationTokenSource? _scanCts;
        private CancellationTokenSource? _symbolCts;
        private string _consoleText = string.Empty;
        private string _statusText = "Idle";
        private string? _selectedSymbol;
        private bool _isScanning;

        public ScannerViewModel Scanner => _scannerViewModel;
        public ChartViewModel Chart => _chartViewModel;

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

        public ICommand StartScanCommand => _startScanCommand;
        public ICommand StopScanCommand => _stopScanCommand;

        public MainViewModel(ScannerViewModel scannerViewModel, ChartViewModel chartViewModel, Dispatcher? dispatcher = null)
        {
            _scannerViewModel = scannerViewModel ?? throw new ArgumentNullException(nameof(scannerViewModel));
            _chartViewModel = chartViewModel ?? throw new ArgumentNullException(nameof(chartViewModel));
            _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;

            _startScanCommand = new RelayCommand(async _ => await StartScanAsync(), _ => !IsScanning);
            _stopScanCommand = new RelayCommand(_ => StopScan(), _ => IsScanning);
        }

        private bool IsScanning
        {
            get => _isScanning;
            set
            {
                if (SetProperty(ref _isScanning, value, nameof(IsScanning)))
                {
                    _dispatcher.InvokeAsync(() =>
                    {
                        _startScanCommand.RaiseCanExecuteChanged();
                        _stopScanCommand.RaiseCanExecuteChanged();
                    });
                }
            }
        }

        private async Task StartScanAsync()
        {
            if (IsScanning)
            {
                return;
            }

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
            {
                return;
            }

            _scanCts?.Cancel();
        }

        private async Task LoadSelectedSymbolAsync(string? symbol)
        {
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
                await _chartViewModel.LoadChartForSymbolAsync(symbol, token).ConfigureAwait(false);
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

        private void Log(string message)
        {
            var timestamped = $"[{DateTime.Now:HH:mm:ss}] {message}";
            Logger.WriteLine(timestamped);

            _dispatcher.InvokeAsync(() =>
            {
                if (_consoleBuilder.Length > 0)
                {
                    _consoleBuilder.AppendLine();
                }

                _consoleBuilder.Append(timestamped);
                ConsoleText = _consoleBuilder.ToString();
            });
        }

        private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
            {
                return false;
            }

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
