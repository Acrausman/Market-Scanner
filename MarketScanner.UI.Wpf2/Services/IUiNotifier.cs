using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketScanner.UI.Wpf.Services
{
    public interface IUiNotifier
    {
        Task ShowStatusAsync(string message);
        Task FlashButtonAsync(string key);
        Task ShowSnackbarAsync(string message);
    }
}
