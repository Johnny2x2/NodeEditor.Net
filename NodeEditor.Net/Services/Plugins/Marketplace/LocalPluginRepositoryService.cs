using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodeEditor.Net.Services.Plugins;
using NodeEditor.Net.Services.Plugins.Marketplace.Models;

namespace NodeEditor.Net.Services.Plugins.Marketplace;

public sealed class LocalPluginRepositoryService : ILocalPluginRepositoryService
{
    private readonly MarketplaceOptions _options;
    private readonly ILogger<LocalPluginRepositoryService> _logger;

    public LocalPluginRepositoryService(
        IOptions<MarketplaceOptions> options,
        ILogger<LocalPluginRepositoryService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PluginRepositoryAddResult> AddPackageAsync(
        Stream packageStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (packageStream is null)
        {
            return new PluginRepositoryAddResult(false, ErrorMessage: "No package stream provided.");
        }

        if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return new PluginRepositoryAddResult(false, ErrorMessage: "Only .zip plugin packages are supported.");
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"plugin-upload-{Guid.NewGuid():N}.zip");

        try
        {
            await using (var fs = File.Create(tempPath))
            {
                await packageStream.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
            }

            var tempInfo = new FileInfo(tempPath);
            if (tempInfo.Length > _options.MaxUploadSizeBytes)
            {
                return new PluginRepositoryAddResult(
                    false,
                    ErrorMessage: $"Package exceeds the maximum upload size of {_options.MaxUploadSizeBytes / (1024L * 1024L)} MB.");
            }

            var manifest = await ReadManifestFromZipAsync(tempPath, cancellationToken).ConfigureAwait(false);
            if (manifest is null)
            {
                return new PluginRepositoryAddResult(false, ErrorMessage: "No plugin.json was found in the uploaded ZIP.");
            }

            var repositoryPath = GetRepositoryPath();
            Directory.CreateDirectory(repositoryPath);

            var safePluginId = SanitizeFileName(manifest.Id);
            if (string.IsNullOrWhiteSpace(safePluginId))
            {
                return new PluginRepositoryAddResult(false, ErrorMessage: "Plugin manifest has an invalid id.");
            }

            foreach (var existingPath in Directory.EnumerateFileSystemEntries(repositoryPath))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var existingManifest = await TryReadManifestAsync(existingPath, cancellationToken).ConfigureAwait(false);
                if (existingManifest is null)
                {
                    continue;
                }

                if (!string.Equals(existingManifest.Id, manifest.Id, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                TryDeletePath(existingPath);
            }

            var targetPath = Path.Combine(repositoryPath, $"{safePluginId}.zip");
            File.Copy(tempPath, targetPath, overwrite: true);

            return new PluginRepositoryAddResult(
                true,
                PluginId: manifest.Id,
                PluginName: manifest.Name,
                Version: manifest.Version,
                StoredPath: targetPath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add plugin package to local repository.");
            return new PluginRepositoryAddResult(false, ErrorMessage: ex.Message);
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    public async Task<PluginRepositoryDeleteResult> DeletePluginAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return new PluginRepositoryDeleteResult(false, "Plugin id is required.");
        }

        try
        {
            var repositoryPath = GetRepositoryPath();
            if (!Directory.Exists(repositoryPath))
            {
                return new PluginRepositoryDeleteResult(false, "Local repository path does not exist.");
            }

            foreach (var entry in Directory.EnumerateFileSystemEntries(repositoryPath))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var manifest = await TryReadManifestAsync(entry, cancellationToken).ConfigureAwait(false);
                if (manifest is null)
                {
                    continue;
                }

                if (!string.Equals(manifest.Id, pluginId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                TryDeletePath(entry);
                return new PluginRepositoryDeleteResult(true, DeletedPath: entry);
            }

            return new PluginRepositoryDeleteResult(false, "Plugin was not found in local repository.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete plugin {PluginId} from local repository.", pluginId);
            return new PluginRepositoryDeleteResult(false, ex.Message);
        }
    }

    private string GetRepositoryPath()
    {
        if (Path.IsPathRooted(_options.LocalRepositoryPath))
        {
            return _options.LocalRepositoryPath;
        }

        return Path.Combine(AppContext.BaseDirectory, _options.LocalRepositoryPath);
    }

    private static async Task<PluginManifest?> ReadManifestFromZipAsync(string zipPath, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var manifestEntry = archive.Entries.FirstOrDefault(e =>
            string.Equals(Path.GetFileName(e.FullName), "plugin.json", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(e.Name));

        if (manifestEntry is null)
        {
            return null;
        }

        await using var stream = manifestEntry.Open();
        return await JsonSerializer.DeserializeAsync<PluginManifest>(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<PluginManifest?> TryReadManifestAsync(string path, CancellationToken cancellationToken)
    {
        if (Directory.Exists(path))
        {
            var manifestPath = Path.Combine(path, "plugin.json");
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            await using var stream = File.OpenRead(manifestPath);
            return await JsonSerializer.DeserializeAsync<PluginManifest>(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        if (File.Exists(path) && path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return await ReadManifestFromZipAsync(path, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var filtered = new string(value.Where(ch => !invalidChars.Contains(ch)).ToArray());
        return filtered.Trim();
    }

    private static void TryDeletePath(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
