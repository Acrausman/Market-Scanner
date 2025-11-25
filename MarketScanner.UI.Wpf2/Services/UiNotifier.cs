using System;

namespace MarketScanner.UI.Wpf.Services
{
    public class UiNotifier
    {
        public event Action<string> OnNotify;
        public void Show(string message)
        {
            OnNotify?.Invoke(message);
        }
    }
}