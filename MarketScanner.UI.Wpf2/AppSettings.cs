using System;
using System.IO;
using Newtonsoft.Json;

namespace MarketScanner.UI.Wpf
{
    public class AppSettings
    {
        public string NotificationEmail { get; set; } = "";
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
                Console.WriteLine($"[Settings] Failed to save settings: {ex.Message}");
            }
        }
    }
}
