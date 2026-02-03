using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodeEditor.Blazor.Services.Plugins.Marketplace.Models;
using NodeEditor.Blazor.Services.Plugins;

namespace NodeEditor.Blazor.Services.Plugins.Marketplace;

/// <summary>
/// Manages plugin installation, uninstallation, and state tracking.
/// </summary>
public sealed class PluginInstallationService : IPluginInstallationService
{
    private const string InstalledManifestFileName = "installed-plugins.json";

    private readonly PluginOptions _pluginOptions;
    private readonly MarketplaceOptions _marketplaceOptions;
    private readonly PluginLoader _pluginLoader;
    private readonly ILogger<PluginInstallationService> _logger;

    public event EventHandler<PluginInstalledEventArgs>? PluginInstalled;
    public event EventHandler<PluginUninstalledEventArgs>? PluginUninstalled;
    public event EventHandler<PluginUpdatedEventArgs>? PluginUpdated;

    public PluginInstallationService(
        IOptions<PluginOptions> pluginOptions,
        IOptions<MarketplaceOptions> marketplaceOptions,
        PluginLoader pluginLoader,
        ILogger<PluginInstallationService> logger)
    {
        _pluginOptions = pluginOptions.Value;
        _marketplaceOptions = marketplaceOptions.Value;
        _pluginLoader = pluginLoader;
        _logger = logger;
    }

    public async Task<IReadOnlyList<InstalledPluginInfo>> GetInstalledPluginsAsync(
        CancellationToken cancellationToken = default)
    {
        var manifest = await LoadInstalledManifestAsync(cancellationToken).ConfigureAwait(false);
        return manifest.Plugins;
    }

    public async Task<InstalledPluginInfo?> GetInstalledPluginAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        var plugins = await GetInstalledPluginsAsync(cancellationToken).ConfigureAwait(false);
        return plugins.FirstOrDefault(p =>
            string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<PluginInstallResult> InstallAsync(
        IPluginMarketplaceSource source,
        string pluginId,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Installing plugin {PluginId} from {Source}", pluginId, source.SourceId);

        var existing = await GetInstalledPluginAsync(pluginId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return new PluginInstallResult(
                false,
                ErrorMessage: $"Plugin '{pluginId}' is already installed (version {existing.Version}).",
                ErrorCode: PluginInstallErrorCode.AlreadyInstalled);
        }

        var downloadResult = await source.DownloadAsync(pluginId, version, cancellationToken).ConfigureAwait(false);
        if (!downloadResult.Success)
        {
            return new PluginInstallResult(
                false,
                ErrorMessage: downloadResult.ErrorMessage ?? "Download failed.",
                ErrorCode: PluginInstallErrorCode.DownloadFailed);
        }

        return await InstallFromPackageInternalAsync(
            downloadResult.LocalPath,
            downloadResult.PackageStream,
            source.SourceId,
            cancellationToken).ConfigureAwait(false);
    }

    public Task<PluginInstallResult> InstallFromPackageAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        return InstallFromPackageInternalAsync(packagePath, null, null, cancellationToken);
    }

    public async Task<PluginUninstallResult> UninstallAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Uninstalling plugin {PluginId}", pluginId);

        var installed = await GetInstalledPluginAsync(pluginId, cancellationToken).ConfigureAwait(false);
        if (installed is null)
        {
            return new PluginUninstallResult(false, $"Plugin '{pluginId}' is not installed.");
        }

        try
        {
            await _pluginLoader.UnloadPluginAsync(pluginId).ConfigureAwait(false);

            if (Directory.Exists(installed.InstallPath))
            {
                Directory.Delete(installed.InstallPath, true);
            }

            var manifest = await LoadInstalledManifestAsync(cancellationToken).ConfigureAwait(false);
            manifest.Plugins.RemoveAll(p =>
                string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));
            await SaveInstalledManifestAsync(manifest, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully uninstalled plugin {PluginId}", pluginId);

            PluginUninstalled?.Invoke(this, new PluginUninstalledEventArgs
            {
                PluginId = installed.Id,
                PluginName = installed.Name
            });

            return new PluginUninstallResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to uninstall plugin {PluginId}", pluginId);
            return new PluginUninstallResult(false, ex.Message);
        }
    }

    public async Task<PluginInstallResult> UpdateAsync(
        IPluginMarketplaceSource source,
        string pluginId,
        string? targetVersion = null,
        CancellationToken cancellationToken = default)
    {
        var installed = await GetInstalledPluginAsync(pluginId, cancellationToken).ConfigureAwait(false);
        if (installed is null)
        {
            return new PluginInstallResult(
                false,
                ErrorMessage: $"Plugin '{pluginId}' is not installed.",
                ErrorCode: PluginInstallErrorCode.PluginNotFound);
        }

        var previousVersion = installed.Version;

        var uninstallResult = await UninstallAsync(pluginId, cancellationToken).ConfigureAwait(false);
        if (!uninstallResult.Success)
        {
            return new PluginInstallResult(
                false,
                ErrorMessage: uninstallResult.ErrorMessage,
                ErrorCode: PluginInstallErrorCode.Unknown);
        }

        var installResult = await InstallAsync(source, pluginId, targetVersion, cancellationToken).ConfigureAwait(false);

        if (installResult.Success && installResult.Plugin is not null)
        {
            PluginUpdated?.Invoke(this, new PluginUpdatedEventArgs
            {
                Plugin = installResult.Plugin,
                PreviousVersion = previousVersion
            });
        }

        return installResult;
    }

    public async Task<IReadOnlyList<PluginUpdateInfo>> CheckForUpdatesAsync(
        IPluginMarketplaceSource source,
        CancellationToken cancellationToken = default)
    {
        var installed = await GetInstalledPluginsAsync(cancellationToken).ConfigureAwait(false);
        var updates = new List<PluginUpdateInfo>();

        foreach (var plugin in installed)
        {
            var marketplaceInfo = await source.GetDetailsAsync(plugin.Id, cancellationToken).ConfigureAwait(false);
            if (marketplaceInfo is null)
            {
                continue;
            }

            var installedVersion = Version.TryParse(plugin.Version, out var iv) ? iv : new Version(0, 0, 0);
            var availableVersion = Version.TryParse(marketplaceInfo.Version, out var av) ? av : new Version(0, 0, 0);

            if (availableVersion > installedVersion)
            {
                updates.Add(new PluginUpdateInfo(
                    plugin.Id,
                    plugin.Name,
                    plugin.Version,
                    marketplaceInfo.Version,
                    marketplaceInfo.AvailableVersions.FirstOrDefault()?.ReleaseNotes));
            }
        }

        return updates;
    }

    public Task<bool> EnablePluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        return SetPluginEnabledAsync(pluginId, true, cancellationToken);
    }

    public Task<bool> DisablePluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        return SetPluginEnabledAsync(pluginId, false, cancellationToken);
    }

    private async Task<bool> SetPluginEnabledAsync(string pluginId, bool enabled, CancellationToken cancellationToken)
    {
        var manifest = await LoadInstalledManifestAsync(cancellationToken).ConfigureAwait(false);
        var plugin = manifest.Plugins.FirstOrDefault(p =>
            string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));

        if (plugin is null)
        {
            return false;
        }

        var index = manifest.Plugins.IndexOf(plugin);
        manifest.Plugins[index] = plugin with { IsEnabled = enabled };

        await SaveInstalledManifestAsync(manifest, cancellationToken).ConfigureAwait(false);

        if (!enabled)
        {
            await _pluginLoader.UnloadPluginAsync(pluginId).ConfigureAwait(false);
        }
        else
        {
            await _pluginLoader.LoadAndRegisterAsync().ConfigureAwait(false);
        }

        return true;
    }

    private async Task<PluginInstallResult> InstallFromPackageInternalAsync(
        string? packagePath,
        Stream? packageStream,
        string? sourceId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(packagePath) && packageStream is null)
            {
                return new PluginInstallResult(
                    false,
                    ErrorMessage: "No package path or stream provided.",
                    ErrorCode: PluginInstallErrorCode.DownloadFailed);
            }

            var pluginsDir = GetPluginsDirectory();
            Directory.CreateDirectory(pluginsDir);

            string targetDir;

            if (!string.IsNullOrWhiteSpace(packagePath) && packagePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var tempDir = Path.Combine(Path.GetTempPath(), $"plugin-{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempDir);

                if (packageStream is not null)
                {
                    using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read);
                    archive.ExtractToDirectory(tempDir);
                }
                else
                {
                    ZipFile.ExtractToDirectory(packagePath, tempDir);
                }

                var manifestPath = FindManifestRecursive(tempDir);
                if (manifestPath is null)
                {
                    Directory.Delete(tempDir, true);
                    return new PluginInstallResult(
                        false,
                        ErrorMessage: "No plugin.json found in package.",
                        ErrorCode: PluginInstallErrorCode.ManifestInvalid);
                }

                var manifest = await ReadManifestAsync(manifestPath, cancellationToken).ConfigureAwait(false);

                var minApi = ParseVersionSafe(manifest.MinApiVersion);
                if (minApi > _pluginOptions.ApiVersion)
                {
                    Directory.Delete(tempDir, true);
                    return new PluginInstallResult(
                        false,
                        ErrorMessage: $"Plugin requires API {minApi} (host {_pluginOptions.ApiVersion}).",
                        ErrorCode: PluginInstallErrorCode.IncompatibleApiVersion);
                }

                targetDir = Path.Combine(pluginsDir, SanitizeDirectoryName(manifest.Id));

                if (Directory.Exists(targetDir))
                {
                    Directory.Delete(targetDir, true);
                }

                var pluginSourceDir = Path.GetDirectoryName(manifestPath)!;
                Directory.Move(pluginSourceDir, targetDir);

                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }

                return await FinalizeInstallAsync(targetDir, manifest, sourceId, cancellationToken).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(packagePath) && Directory.Exists(packagePath))
            {
                var manifestPath = Path.Combine(packagePath, _marketplaceOptions.ManifestFileName);
                if (!File.Exists(manifestPath))
                {
                    return new PluginInstallResult(
                        false,
                        ErrorMessage: "No plugin.json found in folder.",
                        ErrorCode: PluginInstallErrorCode.ManifestInvalid);
                }

                var manifest = await ReadManifestAsync(manifestPath, cancellationToken).ConfigureAwait(false);

                var minApi = ParseVersionSafe(manifest.MinApiVersion);
                if (minApi > _pluginOptions.ApiVersion)
                {
                    return new PluginInstallResult(
                        false,
                        ErrorMessage: $"Plugin requires API {minApi} (host {_pluginOptions.ApiVersion}).",
                        ErrorCode: PluginInstallErrorCode.IncompatibleApiVersion);
                }

                targetDir = Path.Combine(pluginsDir, SanitizeDirectoryName(manifest.Id));

                if (Directory.Exists(targetDir))
                {
                    Directory.Delete(targetDir, true);
                }

                CopyDirectory(packagePath, targetDir);

                return await FinalizeInstallAsync(targetDir, manifest, sourceId, cancellationToken).ConfigureAwait(false);
            }

            if (packageStream is not null)
            {
                var tempPath = Path.Combine(Path.GetTempPath(), $"plugin-{Guid.NewGuid():N}.zip");
                await using (var fileStream = File.Create(tempPath))
                {
                    await packageStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
                }

                var result = await InstallFromPackageInternalAsync(tempPath, null, sourceId, cancellationToken)
                    .ConfigureAwait(false);

                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // Ignore cleanup errors.
                }

                return result;
            }

            return new PluginInstallResult(
                false,
                ErrorMessage: "Package path does not exist.",
                ErrorCode: PluginInstallErrorCode.DownloadFailed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install plugin from package.");
            return new PluginInstallResult(
                false,
                ErrorMessage: ex.Message,
                ErrorCode: PluginInstallErrorCode.Unknown);
        }
    }

    private async Task<PluginInstallResult> FinalizeInstallAsync(
        string targetDir,
        PluginManifest manifest,
        string? sourceId,
        CancellationToken cancellationToken)
    {
        var installedPlugin = new InstalledPluginInfo
        {
            Id = manifest.Id,
            Name = manifest.Name,
            Version = manifest.Version,
            InstallPath = targetDir,
            InstalledAt = DateTimeOffset.UtcNow,
            SourceId = sourceId,
            IsEnabled = true,
            Category = manifest.Category
        };

        var manifestData = await LoadInstalledManifestAsync(cancellationToken).ConfigureAwait(false);
        manifestData.Plugins.Add(installedPlugin);
        await SaveInstalledManifestAsync(manifestData, cancellationToken).ConfigureAwait(false);

        await _pluginLoader.LoadAndRegisterAsync().ConfigureAwait(false);

        installedPlugin = installedPlugin with { IsLoaded = true };

        _logger.LogInformation("Successfully installed plugin {PluginId} v{Version}",
            installedPlugin.Id, installedPlugin.Version);

        PluginInstalled?.Invoke(this, new PluginInstalledEventArgs { Plugin = installedPlugin });

        return new PluginInstallResult(true, installedPlugin);
    }

    private string GetPluginsDirectory()
    {
        if (Path.IsPathRooted(_pluginOptions.PluginDirectory))
        {
            return _pluginOptions.PluginDirectory;
        }

        return Path.Combine(AppContext.BaseDirectory, _pluginOptions.PluginDirectory);
    }

    private string GetInstalledManifestPath()
    {
        return Path.Combine(GetPluginsDirectory(), InstalledManifestFileName);
    }

    private async Task<InstalledPluginsManifest> LoadInstalledManifestAsync(CancellationToken cancellationToken)
    {
        var path = GetInstalledManifestPath();
        if (!File.Exists(path))
        {
            return new InstalledPluginsManifest();
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<InstalledPluginsManifest>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new InstalledPluginsManifest();
    }

    private async Task SaveInstalledManifestAsync(InstalledPluginsManifest manifest, CancellationToken cancellationToken)
    {
        var path = GetInstalledManifestPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
    }

    private static string? FindManifestRecursive(string directory)
    {
        var manifestPath = Path.Combine(directory, "plugin.json");
        if (File.Exists(manifestPath))
        {
            return manifestPath;
        }

        foreach (var subDir in Directory.EnumerateDirectories(directory))
        {
            var found = FindManifestRecursive(subDir);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static async Task<PluginManifest> ReadManifestAsync(string path, CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<PluginManifest>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Invalid plugin manifest");
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
        }

        foreach (var dir in Directory.EnumerateDirectories(sourceDir))
        {
            CopyDirectory(dir, Path.Combine(targetDir, Path.GetFileName(dir)));
        }
    }

    private static string SanitizeDirectoryName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }

    private static Version ParseVersionSafe(string? version)
    {
        return Version.TryParse(version, out var parsed) ? parsed : new Version(0, 0, 0);
    }

    private sealed class InstalledPluginsManifest
    {
        public int Version { get; set; } = 1;
        public List<InstalledPluginInfo> Plugins { get; set; } = new();
    }
}
