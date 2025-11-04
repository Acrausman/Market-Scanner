using MarketScanner.Data.Diagnostics;
using System;
using System.IO;
using Newtonsoft.Json;
using MarketScanner.Data.Services.Indicators;
using MarketScanner.Core.Enums;

namespace MarketScanner.UI.Wpf
{
    public class AppSettings
    {
        public string NotificationEmail { get; set; } = "";
        public int IndicatorPeriod { get; set; } = 14;
        public RsiSmoothingMethod RsiMethod { get; set; } = RsiSmoothingMethod.Simple;
        public string SelectedTimespan { get; set; } = "3M";
        public int AlertIntervalMinutes { get; set; } = 15;

        private static readonly string SettingsPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MarketScanner", "settings.json");
        public static AppSettings Load()
        {
            try
            {
                if(File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch { /* fallback to defaults */}
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Logger.Error($"[Settings] Failed to save settings: {ex.Message}");
            }
        }
    }
}
