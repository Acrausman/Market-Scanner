using MarketScanner.Core.Configuration;
using MarketScanner.Core.Filtering;
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
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        public static AppSettings Settings { get; private set; }
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            AllocConsole();

            var dispatcher = Dispatcher.CurrentDispatcher;

            // Dependency setup
            const string apiKey = "YISIR_KLqJAdX7U6ix6Pjkyx70C_QgpI";
            var provider = new PolygonMarketDataProvider(apiKey);
            IChartService chartService = new ChartManager();

            Settings = AppSettings.Load();
            var emailService = new EmailService();
            var alertService = new AlertService();
            var alertManager = new AlertManager(alertService,emailService);

            var scannerService = new EquityScannerService(provider, alertManager, Settings);

            var chartViewModel = new ChartViewModel(provider, chartService, dispatcher);
            var scannerViewModel = new ScannerViewModel(scannerService, dispatcher);
 
            var mainViewModel = new MainViewModel(scannerViewModel, chartViewModel,emailService, dispatcher, alertManager, Settings);

            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            mainWindow.Show();
        }
    }
}
