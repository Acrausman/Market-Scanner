using MarketScanner.Core.Configuration;
using MarketScanner.Core.Filtering;
using MarketScanner.Core.Metadata;
using MarketScanner.Core.Models;
using MarketScanner.Data.Providers;
using MarketScanner.Data.Providers.Finnhub;
using MarketScanner.Data.Services;
using MarketScanner.Data.Services.Analysis;
using MarketScanner.UI.Views;
using MarketScanner.UI.Wpf.Services;
using MarketScanner.UI.Wpf.ViewModels;
using System;
using System.Windows;
using System.Windows.Threading;

namespace MarketScanner.UI
{
    public partial class App : Application
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        public static AppSettings Settings { get; private set; }
        public UiNotifier Notifier { get; private set; }
        public App()
        {
            Notifier = new UiNotifier();
        }
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            AllocConsole();

            var dispatcher = Dispatcher.CurrentDispatcher;

            // Dependency setup
            const string apiKey = "YISIR_KLqJAdX7U6ix6Pjkyx70C_QgpI";
            const string finnApiKey = "d44drfhr01qt371uia8gd44drfhr01qt371uia90";
            var provider = new PolygonMarketDataProvider(apiKey);
            var fundamentalProvider = new FinnhubFundamentalProvider(finnApiKey);
            var metadataCache = new TickerMetadataCache(
                System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "ticker_metadata.json"));

            IChartService chartService = new ChartManager();

            Settings = AppSettings.Load();
            var emailService = new EmailService();
            var alertService = new AlertService();
            var alertManager = new AlertManager(alertService,emailService);
            var filterService = new FilterService();

            var scannerService = new EquityScannerService(
                provider,
                fundamentalProvider,
                metadataCache,
                alertManager,
                Settings);

            var chartViewModel = new ChartViewModel(provider, chartService, dispatcher);
            var filterPanelViewModel = new FilterPanelViewModel();
            var alertPanelViewModel = new AlertPanelViewModel();
            var scannerViewModel = new ScannerViewModel(scannerService, dispatcher);

            var mainViewModel = new MainViewModel(
                scannerViewModel,
                chartViewModel,
                filterPanelViewModel,
                alertPanelViewModel,
                emailService,
                filterService,
                dispatcher,
                alertManager,
                Settings,
                Notifier,
                metadataCache,
                scannerService);

            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            mainWindow.Show();
        }
    }
}
