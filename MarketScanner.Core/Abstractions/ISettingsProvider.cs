using MarketScanner.Core.Models;

namespace MarketScanner.Core.Abstractions;

/// <summary>
/// Contract for providing immutable snapshots of user-configurable scanner settings. Implement this in outer layers that handle persistence.
/// </summary>
public interface ISettingsProvider
{
    /// <summary>
    /// Retrieves the current scanner settings as defined by the host application or user preferences.
    /// </summary>
    /// <param name="cancellationToken">Token that allows the caller to cancel the retrieval operation.</param>
    /// <returns>A task that resolves to the latest <see cref="ScannerSettings"/> instance.</returns>
    ValueTask<ScannerSettings> GetScannerSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists updated scanner settings supplied by business logic components.
    /// </summary>
    /// <param name="settings">Settings snapshot to persist.</param>
    /// <param name="cancellationToken">Token that allows the caller to cancel the persistence operation.</param>
    /// <returns>A task that completes when the settings have been stored.</returns>
    ValueTask SaveScannerSettingsAsync(ScannerSettings settings, CancellationToken cancellationToken = default);
}
