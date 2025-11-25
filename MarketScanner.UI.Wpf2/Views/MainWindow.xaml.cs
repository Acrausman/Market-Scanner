using System.Windows;
using System.Runtime.InteropServices;
using System.Windows.Media.Animation;
using System;
using System.Threading.Tasks;

namespace MarketScanner.UI.Views
{
    public partial class MainWindow : Window
    {
        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        public MainWindow()
        {
            InitializeComponent();
            AllocConsole();

            ((App)Application.Current).Notifier.OnNotify += ShowSnackbar;
        }

        private async void ShowSnackbar(string message)
        {
            SnackbarMessage.Text = message;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            await Task.Delay(1500);
            var fadeout = new DoubleAnimation(1,0, TimeSpan.FromMilliseconds(1000));
            Snackbar.BeginAnimation(OpacityProperty, fadeout);
        }
    }
}
