using System;
using System.Threading.Tasks;

namespace MarketScanner.UI.Wpf.Services
{
    public class UiNotifier : IUiNotifier
    {
        public event Action<string>? OnNotify;
        public Task ShowSnackbarAsync(string message)
        {
            OnNotify?.Invoke(message);
            return Task.CompletedTask;
        }

        public Task ShowStatusAsync(string message)
        {
            OnNotify?.Invoke(message);
            return Task.CompletedTask;
        }
        public Task FlashButtonAsync(string key)
        {
            return Task.CompletedTask;
        }
    }
}