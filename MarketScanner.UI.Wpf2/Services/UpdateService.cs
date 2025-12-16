using MarketScanner.Data.Diagnostics;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Xps.Serialization;

namespace MarketScanner.UI.Wpf.Services
{
    public class UpdateService
    {
        private const string RepoOwner = "Acrausman";
        private const string RepoName = "Market-Scanner";
        private const string VersionUrl = "https://raw.githubusercontent.com/Acrausman/Market-Scanner/refs/heads/master/latest_version.txt";

        public async Task CheckForUpdatesAsync()
        {
            Logger.WriteLine("Checking for updates");
            try
            {
                using var client = new HttpClient();
                string latestVersionText = await client.GetStringAsync(VersionUrl);

                Version latestVersion = new Version(latestVersionText.Trim());
                Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version ??
                    new Version("0.0.0");
                Logger.WriteLine($"Checking current version (v{currentVersion}) with latest version (v{latestVersion})");
                if(latestVersion > currentVersion)
                {
                    string versionTag = $"v{latestVersion}";
                    string installerUrl =
                        $"https://github.com/{RepoOwner}/{RepoName}/releases/download/{versionTag}/CentSenseSetup.exe";       
                    string hashUrl =
                        $"https://github.com/{RepoOwner}/{RepoName}/releases/download/{versionTag}/CentSenseSetup.sha256";
                    bool shouldInstall = false;
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var result = System.Windows.MessageBox.Show(
                            $"A new version of MarketScanner is available!\n\n" +
                            $"Current Version: {currentVersion}\n" +
                            $"Latest Version: {latestVersion}\n" +
                            $"Would you like to download and install it now?",
                            "Update Available",
                            System.Windows.MessageBoxButton.YesNo,
                            System.Windows.MessageBoxImage.Information);

                        if (result == System.Windows.MessageBoxResult.Yes)
                            shouldInstall = true;
                    });
                    if (shouldInstall) 
                        await DownloadAndInstallAsync(installerUrl);
                }
                else
                {
                    Logger.WriteLine($"Program is up-to-date on version {currentVersion}");
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"[UPDATER] Update check failed: {ex.Message}");
            }
        }
        private async Task DownloadAndInstallAsync(string url)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), "CentSenseSetup.exe");
            Logger.WriteLine($"Downloading from {url} to {tempPath}");

            try
            {
                using var client = new HttpClient();

                var bytes = await client.GetByteArrayAsync(url);

                string hashUrl = url.Replace(".exe", ".sha256");
                string expectedHash = (await client.GetStringAsync(hashUrl))
                    .Trim()
                    .ToLowerInvariant();

                string actualHash = ComputeSha256(bytes);

                if (actualHash != expectedHash)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        System.Windows.MessageBox.Show(
                            "The update file could not be verified and will not be installed.\n\n" +
                            "Please try again later.",
                            "Update Verification Failed",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                    });

                    return;
                }

                await File.WriteAllBytesAsync(tempPath, bytes);

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = tempPath,
                        UseShellExecute = true
                    };

                    Process.Start(psi);

                    System.Windows.Application.Current.Shutdown();
                });
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    System.Windows.MessageBox.Show(
                        "Failed to download update:\n" + ex.Message,
                        "Update Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                });
            }
        }

        private async Task LaunchInstallerAndExitAsync(string installerPath)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName = installerPath,
                    UseShellExecute = true
                };

                Process.Start(psi);

                Application.Current.Shutdown();
            });
        }
        private static string ComputeSha256(byte[] data)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha.ComputeHash(data);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
    }
}
