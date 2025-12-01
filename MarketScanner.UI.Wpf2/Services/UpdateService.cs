using MarketScanner.Data.Diagnostics;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace MarketScanner.UI.Wpf.Services
{
    public class UpdateService
    {
        private const string VersionUrl = "https://raw.githubusercontent.com/Acrausman/Market-Scanner/refs/heads/master/latest_version.txt";
        private const string InstallerUrl = "https://github.com/Acrausman/Market-Scanner/releases/download/v0.8/MarketScannerSetup.exe";

        public async Task CheckForUpdatesAsync()
        {
            try
            {
                using var client = new HttpClient();
                string latestVersionText = await client.GetStringAsync(VersionUrl);

                Version latestVersion = new Version(latestVersionText.Trim());
                Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version ??
                    new Version("0.0.0");

                if(latestVersion > currentVersion)
                {
                    var result = System.Windows.MessageBox.Show(
                        $"A new version of MarketScanner is available!\n\n" +
                        $"Current Version: {currentVersion}\n" +
                        $"Latest Version: {latestVersion}\n" +
                        $"Would you like to download and install it now?",
                        "Update Available",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Information);

                    if(result == System.Windows.MessageBoxResult.Yes)
                    {
                        await DownloadAndInstallAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"[UPDATER] Update check failed: {ex.Message}");
            }
        }

        private async Task DownloadAndInstallAsync()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), "MarketScannerSetup.exe");
            
            try
            {
                using var client = new HttpClient();
                var bytes = await client.GetByteArrayAsync(InstallerUrl);
                await File.WriteAllBytesAsync(tempPath, bytes);

                Process.Start(new ProcessStartInfo
                {
                    FileName = tempPath,
                    UseShellExecute = true
                });
                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    "Failed to download update:\n" + ex.Message,
                    "Update Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
