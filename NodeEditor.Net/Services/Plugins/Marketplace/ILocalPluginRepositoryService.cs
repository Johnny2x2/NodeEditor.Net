using NodeEditor.Net.Services.Plugins.Marketplace.Models;

namespace NodeEditor.Net.Services.Plugins.Marketplace;

/// <summary>
/// Manages mutable operations on the local plugin repository.
/// </summary>
public interface ILocalPluginRepositoryService
{
    Task<PluginRepositoryAddResult> AddPackageAsync(
        Stream packageStream,
        string fileName,
        CancellationToken cancellationToken = default);

    Task<PluginRepositoryDeleteResult> DeletePluginAsync(
        string pluginId,
        CancellationToken cancellationToken = default);
}
