using System;
using System.IO;
using MarketScanner.Core.Abstractions;
using MarketScanner.Core.Enums;
using Newtonsoft.Json;

namespace MarketScanner.Core.Configuration;

/// <summary>
/// Provides strongly-typed access to persisted application configuration shared across Market Scanner projects.
/// </summary>
/// <remarks>
/// UI layers should call <see cref="Load"/> during startup to obtain a mutable instance and, after applying user changes,
/// invoke <see cref="Save(IAppLogger?)"/> (optionally off the UI thread) to persist updates while passing an
/// <see cref="IAppLogger"/> implementation when available to capture serialization errors.
/// </remarks>
public class AppSettings
{
    /// <summary>
    /// Gets or sets the notification email address used for scheduled alerts.
    /// </summary>
    public string NotificationEmail { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of periods applied when calculating RSI-based indicators.
    /// </summary>
    public int IndicatorPeriod { get; set; } = 14;

    /// <summary>
    /// Gets or sets the smoothing method used when computing RSI values.
    /// </summary>
    public RsiSmoothingMethod RsiMethod { get; set; } = RsiSmoothingMethod.Simple;

    /// <summary>
    /// Gets or sets the preferred chart timespan selection, expressed using Polygon's range tokens (e.g. "3M").
    /// </summary>
    public string SelectedTimespan { get; set; } = "3M";

    /// <summary>
    /// Gets or sets the desired interval, in minutes, between alert notifications.
    /// </summary>
    public int AlertIntervalMinutes { get; set; } = 15;

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MarketScanner",
        "settings.json");

    /// <summary>
    /// Loads persisted configuration from disk, returning defaults when the stored file is missing or unreadable.
    /// </summary>
    /// <returns>An <see cref="AppSettings"/> instance populated from disk or with default values.</returns>
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

    /// <summary>
    /// Persists the current settings to disk for reuse in future sessions.
    /// </summary>
    /// <param name="logger">Optional application logger for recording persistence errors.</param>
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
