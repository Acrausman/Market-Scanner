using MarketScanner.Data.Providers;
using MarketScanner.Data.Services;
using MarketScanner.UI.Views;
using MarketScanner.UI.Wpf.Services;
using MarketScanner.UI.Wpf.ViewModels;
using System.Windows;
using System.Windows.Threading;

namespace MarketScanner.UI
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var dispatcher = Dispatcher.CurrentDispatcher;

            // Dependency setup
            const string apiKey = "YISIR_KLqJAdX7U6ix6Pjkyx70C_QgpI";
            var provider = new PolygonMarketDataProvider(apiKey);
            IChartService chartService = new ChartManager();
            IEquityScannerService scannerService = new EquityScannerService(provider);

            var chartViewModel = new ChartViewModel(provider, chartService, dispatcher);
            var scannerViewModel = new ScannerViewModel(scannerService, dispatcher);
            var emailService = new EmailService();
            var mainViewModel = new MainViewModel(scannerViewModel, chartViewModel,emailService, dispatcher);

            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            mainWindow.Show();
        }
    }
}
