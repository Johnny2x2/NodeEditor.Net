using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodeEditor.Net.Services.Plugins.Marketplace.Models;
using NodeEditor.Net.Services.Plugins;

namespace NodeEditor.Net.Services.Plugins.Marketplace;

/// <summary>
/// Manages plugin installation, uninstallation, and state tracking.
/// </summary>
public sealed class PluginInstallationService : IPluginInstallationService
{
    private const string InstalledManifestFileName = "installed-plugins.json";
    private const string DisabledMarkerFileName = ".plugin-disabled";

    private readonly PluginOptions _pluginOptions;
    private readonly MarketplaceOptions _marketplaceOptions;
    private readonly IPluginLoader _pluginLoader;
    private readonly ILogger<PluginInstallationService> _logger;

    public event EventHandler<PluginInstalledEventArgs>? PluginInstalled;
    public event EventHandler<PluginUninstalledEventArgs>? PluginUninstalled;
    public event EventHandler<PluginUpdatedEventArgs>? PluginUpdated;

    public PluginInstallationService(
        IOptions<PluginOptions> pluginOptions,
        IOptions<MarketplaceOptions> marketplaceOptions,
        IPluginLoader pluginLoader,
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
        try
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install plugin {PluginId} from {Source}", pluginId, source.SourceId);
            return new PluginInstallResult(
                false,
                ErrorMessage: ex.Message,
                ErrorCode: PluginInstallErrorCode.Unknown);
        }
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

            var softUninstall = false;
            string? softUninstallReason = null;

            if (Directory.Exists(installed.InstallPath))
            {
                try
                {
                    Directory.Delete(installed.InstallPath, true);
                }
                catch (IOException ex) when (IsLockingException(ex))
                {
                    softUninstall = true;
                    softUninstallReason = ex.Message;
                    MarkPluginDirectoryDisabled(installed.InstallPath, pluginId, ex.Message);
                }
                catch (UnauthorizedAccessException ex)
                {
                    softUninstall = true;
                    softUninstallReason = ex.Message;
                    MarkPluginDirectoryDisabled(installed.InstallPath, pluginId, ex.Message);
                }
            }

            var manifest = await LoadInstalledManifestAsync(cancellationToken).ConfigureAwait(false);
            manifest.Plugins.RemoveAll(p =>
                string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));
            await SaveInstalledManifestAsync(manifest, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully uninstalled plugin {PluginId}", pluginId);
            if (softUninstall)
            {
                _logger.LogWarning(
                    "Plugin '{PluginId}' was unloaded and disabled, but files are still locked and will be removed later. Details: {Reason}",
                    pluginId,
                    softUninstallReason);
            }

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
                    var prepared = await PrepareTargetDirectoryAsync(targetDir, manifest.Id, cancellationToken).ConfigureAwait(false);
                    if (!prepared.Success)
                    {
                        _logger.LogWarning("Could not replace plugin directory '{TargetDir}'. Falling back to existing directory. {Reason}",
                            targetDir, prepared.ErrorMessage);

                        Directory.Delete(tempDir, true);
                        return await FinalizeInstallUsingExistingDirectoryAsync(targetDir, manifest, sourceId, cancellationToken)
                            .ConfigureAwait(false);
                    }
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
                    var prepared = await PrepareTargetDirectoryAsync(targetDir, manifest.Id, cancellationToken).ConfigureAwait(false);
                    if (!prepared.Success)
                    {
                        _logger.LogWarning("Could not replace plugin directory '{TargetDir}'. Falling back to existing directory. {Reason}",
                            targetDir, prepared.ErrorMessage);

                        return await FinalizeInstallUsingExistingDirectoryAsync(targetDir, manifest, sourceId, cancellationToken)
                            .ConfigureAwait(false);
                    }
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
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Failed to install plugin from package due to permission error.");
            return new PluginInstallResult(
                false,
                ErrorMessage: ex.Message,
                ErrorCode: PluginInstallErrorCode.PermissionDenied);
        }
        catch (IOException ex) when (ex.Message.Contains("access", StringComparison.OrdinalIgnoreCase)
                                     || ex.Message.Contains("denied", StringComparison.OrdinalIgnoreCase)
                                     || ex.Message.Contains("used by another process", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(ex, "Failed to install plugin from package due to file lock.");
            return new PluginInstallResult(
                false,
                ErrorMessage: ex.Message,
                ErrorCode: PluginInstallErrorCode.PermissionDenied);
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
        TryDeleteDisabledMarker(targetDir);

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

    private async Task<PluginInstallResult> FinalizeInstallUsingExistingDirectoryAsync(
        string targetDir,
        PluginManifest expectedManifest,
        string? sourceId,
        CancellationToken cancellationToken)
    {
        var existingManifestPath = Path.Combine(targetDir, _marketplaceOptions.ManifestFileName);
        if (!File.Exists(existingManifestPath))
        {
            _logger.LogWarning("Plugin directory '{TargetDir}' is locked and does not contain '{ManifestFileName}'. Reusing expected manifest metadata.",
                targetDir,
                _marketplaceOptions.ManifestFileName);

            return await FinalizeInstallAsync(targetDir, expectedManifest, sourceId, cancellationToken).ConfigureAwait(false);
        }

        var existingManifest = await ReadManifestAsync(existingManifestPath, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(existingManifest.Id, expectedManifest.Id, StringComparison.OrdinalIgnoreCase))
        {
            return new PluginInstallResult(
                false,
                ErrorMessage: $"Locked plugin directory contains '{existingManifest.Id}', expected '{expectedManifest.Id}'.",
                ErrorCode: PluginInstallErrorCode.ManifestInvalid);
        }

        return await FinalizeInstallAsync(targetDir, existingManifest, sourceId, cancellationToken).ConfigureAwait(false);
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

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<InstalledPluginsManifest>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new InstalledPluginsManifest();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read installed plugin manifest at '{ManifestPath}'. Using empty manifest.", path);
            return new InstalledPluginsManifest();
        }
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

    private static bool IsLockingException(IOException ex)
    {
        return ex.Message.Contains("access", StringComparison.OrdinalIgnoreCase)
               || ex.Message.Contains("denied", StringComparison.OrdinalIgnoreCase)
               || ex.Message.Contains("used by another process", StringComparison.OrdinalIgnoreCase)
               || ex.Message.Contains("cannot access the file", StringComparison.OrdinalIgnoreCase);
    }

    private void MarkPluginDirectoryDisabled(string pluginDirectory, string pluginId, string reason)
    {
        try
        {
            Directory.CreateDirectory(pluginDirectory);
            var markerPath = Path.Combine(pluginDirectory, DisabledMarkerFileName);
            var markerContents = $"PluginId={pluginId}{Environment.NewLine}DisabledAtUtc={DateTimeOffset.UtcNow:O}{Environment.NewLine}Reason={reason}";
            File.WriteAllText(markerPath, markerContents);
        }
        catch (Exception markerEx)
        {
            _logger.LogWarning(markerEx,
                "Failed to mark plugin directory '{PluginDirectory}' as disabled for plugin '{PluginId}'.",
                pluginDirectory,
                pluginId);
        }
    }

    private static void TryDeleteDisabledMarker(string pluginDirectory)
    {
        try
        {
            var markerPath = Path.Combine(pluginDirectory, DisabledMarkerFileName);
            if (File.Exists(markerPath))
            {
                File.Delete(markerPath);
            }
        }
        catch
        {
            // Ignore marker cleanup failures.
        }
    }

    private async Task<(bool Success, string? ErrorMessage)> PrepareTargetDirectoryAsync(
        string targetDir,
        string pluginId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _pluginLoader.UnloadPluginAsync(pluginId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Plugin '{PluginId}' could not be unloaded before install replacement.", pluginId);
        }

        ReleaseFileLocks();

        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (!Directory.Exists(targetDir))
                {
                    return (true, null);
                }

                Directory.Delete(targetDir, true);
                return (true, null);
            }
            catch (IOException ex) when (attempt < maxAttempts)
            {
                _logger.LogDebug(ex, "Retrying plugin directory cleanup (attempt {Attempt}/{MaxAttempts}).", attempt, maxAttempts);
                await Task.Delay(150 * attempt, cancellationToken).ConfigureAwait(false);
                ReleaseFileLocks();
            }
            catch (UnauthorizedAccessException ex) when (attempt < maxAttempts)
            {
                _logger.LogDebug(ex, "Retrying plugin directory cleanup (attempt {Attempt}/{MaxAttempts}).", attempt, maxAttempts);
                await Task.Delay(150 * attempt, cancellationToken).ConfigureAwait(false);
                ReleaseFileLocks();
            }
            catch (IOException ex)
            {
                return (false, $"Plugin '{pluginId}' is in use. Close running sessions and retry install. Details: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                return (false, $"Plugin '{pluginId}' is in use. Close running sessions and retry install. Details: {ex.Message}");
            }
        }

        return (false, $"Plugin '{pluginId}' could not be replaced because files are still locked.");
    }

    private static void ReleaseFileLocks()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private sealed class InstalledPluginsManifest
    {
        public int Version { get; set; } = 1;
        public List<InstalledPluginInfo> Plugins { get; set; } = new();
    }
}
