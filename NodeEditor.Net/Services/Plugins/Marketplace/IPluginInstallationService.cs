using NodeEditor.Net.Services.Plugins.Marketplace.Models;

namespace NodeEditor.Net.Services.Plugins.Marketplace;

/// <summary>
/// Service for managing plugin installation lifecycle.
/// </summary>
public interface IPluginInstallationService
{
    event EventHandler<PluginInstalledEventArgs>? PluginInstalled;
    event EventHandler<PluginUninstalledEventArgs>? PluginUninstalled;
    event EventHandler<PluginUpdatedEventArgs>? PluginUpdated;

    Task<IReadOnlyList<InstalledPluginInfo>> GetInstalledPluginsAsync(
        CancellationToken cancellationToken = default);

    Task<InstalledPluginInfo?> GetInstalledPluginAsync(
        string pluginId,
        CancellationToken cancellationToken = default);

    Task<PluginInstallResult> InstallAsync(
        IPluginMarketplaceSource source,
        string pluginId,
        string? version = null,
        CancellationToken cancellationToken = default);

    Task<PluginInstallResult> InstallFromPackageAsync(
        string packagePath,
        CancellationToken cancellationToken = default);

    Task<PluginUninstallResult> UninstallAsync(
        string pluginId,
        CancellationToken cancellationToken = default);

    Task<PluginInstallResult> UpdateAsync(
        IPluginMarketplaceSource source,
        string pluginId,
        string? targetVersion = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PluginUpdateInfo>> CheckForUpdatesAsync(
        IPluginMarketplaceSource source,
        CancellationToken cancellationToken = default);

    Task<bool> EnablePluginAsync(string pluginId, CancellationToken cancellationToken = default);

    Task<bool> DisablePluginAsync(string pluginId, CancellationToken cancellationToken = default);
}
