using NodeEditor.Blazor.Services.Plugins.Marketplace.Models;

namespace NodeEditor.Blazor.Services.Plugins.Marketplace;

/// <summary>
/// Abstraction for a plugin marketplace data source.
/// Implementations can be local (folder-based) or remote (HTTP API).
/// </summary>
public interface IPluginMarketplaceSource
{
    /// <summary>
    /// Unique identifier for this source (e.g., "local", "official-marketplace").
    /// </summary>
    string SourceId { get; }

    /// <summary>
    /// Human-readable name for display.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Search for plugins matching the query.
    /// </summary>
    Task<IReadOnlyList<MarketplacePluginInfo>> SearchAsync(
        string? query = null,
        string? category = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get detailed information about a specific plugin.
    /// </summary>
    Task<MarketplacePluginInfo?> GetDetailsAsync(
        string pluginId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Download a plugin package as a stream or local path.
    /// </summary>
    Task<PluginDownloadResult> DownloadAsync(
        string pluginId,
        string? version = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all available categories from this source.
    /// </summary>
    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if this source is available/reachable.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
