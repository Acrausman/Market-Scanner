using System;
using System.IO;
using MarketScanner.Core.Abstractions;
using MarketScanner.Core.Enums;
using Newtonsoft.Json;

namespace MarketScanner.Core.Configuration;

public class AppSettings
{
    public double FilterMinPrice { get; set; } = 0;
    public double FilterMaxPrice { get; set; } = 99999;
    public string FilterSector { get; set; } = "Any";
    public string FilterCountry { get; set; } = "Any";
    public string NotificationEmail { get; set; } = string.Empty;

    public int IndicatorPeriod { get; set; } = 14;

    public RsiSmoothingMethod RsiMethod { get; set; } = RsiSmoothingMethod.Simple;

    public string SelectedTimespan { get; set; } = "3M";

    public int AlertIntervalMinutes { get; set; } = 15;

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MarketScanner",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            // Swallow deserialization errors and fall back to defaults to keep the application running.
        }

        return new AppSettings();
    }

    public void Save(IAppLogger? logger = null)
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
        catch (Exception ex)
        {
            logger?.Log(LogSeverity.Error, $"[Settings] Failed to save settings: {ex.Message}", ex);
        }
    }
}
