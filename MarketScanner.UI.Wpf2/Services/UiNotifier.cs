using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace MarketScanner.UI.Wpf.Services
{
    public class UiNotifier : IUiNotifier, INotifyPropertyChanged
    {
        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            private set { _statusMessage = value; OnPropertyChanged(nameof(StatusMessage)); }
        }

        private string _snackbarMessage = "";
        public string SnackbarMessage
        {
            get => _snackbarMessage;
            private set
            {
                _snackbarMessage = value;
                OnPropertyChanged(nameof(SnackbarMessage));
            }
        }

        private readonly Dictionary<string, (string text, Brush brush)> _buttonState
            = new();
        public async Task ShowStatusAsync(string message)
        {
            StatusMessage = message;
            await Task.Delay(2000);
            StatusMessage = "";
        }

        public async Task FlashButtonAsync(string key)
        {
            _buttonState[key] = ("Saved!", Brushes.LawnGreen);
            OnPropertyChanged(nameof(ButtonStates));
        }
        public Task ShowSnackbarAsync(string message)
        {
            return ShowStatusAsync(message);
        }

        public Dictionary<string, (string text, Brush brush)> ButtonStates => _buttonState;
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string p)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
    
}
