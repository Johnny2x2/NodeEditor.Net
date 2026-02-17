using Microsoft.Extensions.Logging;
using NodeEditor.Net.Services.Plugins.Marketplace.Models;

namespace NodeEditor.Net.Services.Plugins.Marketplace;

/// <summary>
/// Aggregates multiple marketplace sources into a single unified source.
/// </summary>
public sealed class AggregatedPluginMarketplaceSource : IPluginMarketplaceSource
{
    private readonly IEnumerable<IPluginMarketplaceSource> _sources;
    private readonly ILogger<AggregatedPluginMarketplaceSource> _logger;

    public string SourceId => "aggregated";
    public string DisplayName => "All Sources";

    public AggregatedPluginMarketplaceSource(
        IEnumerable<IPluginMarketplaceSource> sources,
        ILogger<AggregatedPluginMarketplaceSource> logger)
    {
        _sources = sources.Where(s => s.SourceId != SourceId).ToList();
        _logger = logger;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var tasks = _sources.Select(s => s.IsAvailableAsync(cancellationToken));
        var results = await Task.WhenAll(tasks);
        return results.Any(r => r);
    }

    public async Task<IReadOnlyList<MarketplacePluginInfo>> SearchAsync(
        string? query = null,
        string? category = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var allPlugins = new Dictionary<string, MarketplacePluginInfo>(StringComparer.OrdinalIgnoreCase);

        var tasks = _sources.Select(async source =>
        {
            try
            {
                if (!await source.IsAvailableAsync(cancellationToken))
                    return Enumerable.Empty<MarketplacePluginInfo>();

                return await source.SearchAsync(query, category, tags, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to search source {SourceId}", source.SourceId);
                return Enumerable.Empty<MarketplacePluginInfo>();
            }
        });

        var results = await Task.WhenAll(tasks);

        foreach (var plugins in results)
        {
            foreach (var plugin in plugins)
            {
                if (allPlugins.TryGetValue(plugin.Id, out var existing))
                {
                    var existingVersion = Version.TryParse(existing.Version, out var ev) ? ev : new Version(0, 0, 0);
                    var newVersion = Version.TryParse(plugin.Version, out var nv) ? nv : new Version(0, 0, 0);

                    if (newVersion > existingVersion)
                    {
                        allPlugins[plugin.Id] = plugin;
                    }
                }
                else
                {
                    allPlugins[plugin.Id] = plugin;
                }
            }
        }

        return allPlugins.Values
            .OrderBy(p => p.Name)
            .ToList();
    }

    public async Task<MarketplacePluginInfo?> GetDetailsAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        foreach (var source in _sources)
        {
            try
            {
                if (!await source.IsAvailableAsync(cancellationToken))
                    continue;

                var plugin = await source.GetDetailsAsync(pluginId, cancellationToken);
                if (plugin is not null)
                    return plugin;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get details from source {SourceId}", source.SourceId);
            }
        }

        return null;
    }

    public async Task<PluginDownloadResult> DownloadAsync(
        string pluginId,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        foreach (var source in _sources)
        {
            try
            {
                if (!await source.IsAvailableAsync(cancellationToken))
                    continue;

                var plugin = await source.GetDetailsAsync(pluginId, cancellationToken);
                if (plugin is not null)
                {
                    return await source.DownloadAsync(pluginId, version, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to download from source {SourceId}", source.SourceId);
            }
        }

        return new PluginDownloadResult(false, ErrorMessage: $"Plugin '{pluginId}' not found in any source.");
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var allCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var tasks = _sources.Select(async source =>
        {
            try
            {
                if (!await source.IsAvailableAsync(cancellationToken))
                    return Enumerable.Empty<string>();

                return await source.GetCategoriesAsync(cancellationToken);
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        });

        var results = await Task.WhenAll(tasks);

        foreach (var categories in results)
        {
            foreach (var category in categories)
            {
                allCategories.Add(category);
            }
        }

        return allCategories.OrderBy(c => c).ToList();
    }
}
