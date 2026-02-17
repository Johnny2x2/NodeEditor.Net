using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodeEditor.Net.Services.Plugins.Marketplace.Models;
using NodeEditor.Net.Services.Plugins;

namespace NodeEditor.Net.Services.Plugins.Marketplace;

/// <summary>
/// Local file system-based plugin marketplace source.
/// Scans a local folder for plugin packages (folders with plugin.json or .zip files).
/// </summary>
public sealed class LocalPluginMarketplaceSource : IPluginMarketplaceSource
{
    private const string ExtendedManifestFileName = "plugin-marketplace.json";

    private readonly MarketplaceOptions _options;
    private readonly ILogger<LocalPluginMarketplaceSource> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public string SourceId => "local";
    public string DisplayName => "Local Repository";

    public LocalPluginMarketplaceSource(
        IOptions<MarketplaceOptions> options,
        ILogger<LocalPluginMarketplaceSource> logger)
    {
        _options = options.Value;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var path = GetRepositoryPath();
        return Task.FromResult(Directory.Exists(path));
    }

    public async Task<IReadOnlyList<MarketplacePluginInfo>> SearchAsync(
        string? query = null,
        string? category = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var plugins = await ScanRepositoryAsync(cancellationToken).ConfigureAwait(false);

        var filtered = plugins.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            filtered = filtered.Where(p =>
                p.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || p.Id.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (p.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (p.Author?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || p.Tags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            filtered = filtered.Where(p =>
                string.Equals(p.Category, category, StringComparison.OrdinalIgnoreCase));
        }

        if (tags is not null)
        {
            var tagList = tags.ToList();
            if (tagList.Count > 0)
            {
                filtered = filtered.Where(p =>
                    tagList.All(t => p.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)));
            }
        }

        return filtered
            .OrderBy(p => p.Name)
            .ToList();
    }

    public async Task<MarketplacePluginInfo?> GetDetailsAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        var plugins = await ScanRepositoryAsync(cancellationToken).ConfigureAwait(false);
        return plugins.FirstOrDefault(p =>
            string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<PluginDownloadResult> DownloadAsync(
        string pluginId,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        var repoPath = GetRepositoryPath();
        if (!Directory.Exists(repoPath))
        {
            return new PluginDownloadResult(false, ErrorMessage: "Repository folder does not exist.");
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(repoPath))
        {
            var manifest = await TryReadManifestAsync(entry, cancellationToken).ConfigureAwait(false);
            if (manifest is null)
            {
                continue;
            }

            if (!string.Equals(manifest.Id, pluginId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return new PluginDownloadResult(
                Success: true,
                LocalPath: entry,
                Version: manifest.Version);
        }

        return new PluginDownloadResult(false, ErrorMessage: $"Plugin '{pluginId}' not found in repository.");
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var plugins = await ScanRepositoryAsync(cancellationToken).ConfigureAwait(false);
        return plugins
            .Where(p => !string.IsNullOrWhiteSpace(p.Category))
            .Select(p => p.Category!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();
    }

    private string GetRepositoryPath()
    {
        if (Path.IsPathRooted(_options.LocalRepositoryPath))
        {
            return _options.LocalRepositoryPath;
        }

        return Path.Combine(AppContext.BaseDirectory, _options.LocalRepositoryPath);
    }

    private async Task<List<MarketplacePluginInfo>> ScanRepositoryAsync(CancellationToken cancellationToken)
    {
        var repoPath = GetRepositoryPath();
        var plugins = new List<MarketplacePluginInfo>();

        if (!Directory.Exists(repoPath))
        {
            _logger.LogWarning("Local repository path does not exist: {Path}", repoPath);
            return plugins;
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(repoPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var manifest = await TryReadManifestAsync(entry, cancellationToken).ConfigureAwait(false);
                if (manifest is null)
                {
                    continue;
                }

                var extended = await TryReadExtendedManifestAsync(entry, cancellationToken).ConfigureAwait(false);

                var plugin = CreatePluginInfo(manifest, extended, entry);
                plugins.Add(plugin);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read plugin from {Entry}", entry);
            }
        }

        return plugins;
    }

    private async Task<PluginManifest?> TryReadManifestAsync(string path, CancellationToken cancellationToken)
    {
        string? manifestJson = null;

        if (Directory.Exists(path))
        {
            var manifestPath = Path.Combine(path, _options.ManifestFileName);
            if (File.Exists(manifestPath))
            {
                manifestJson = await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false);
            }
        }
        else if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = ZipFile.OpenRead(path);
            var manifestEntry = archive.Entries.FirstOrDefault(e =>
                string.Equals(Path.GetFileName(e.FullName), _options.ManifestFileName, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(e.Name));

            if (manifestEntry is not null)
            {
                using var stream = manifestEntry.Open();
                using var reader = new StreamReader(stream);
                manifestJson = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        if (string.IsNullOrWhiteSpace(manifestJson))
        {
            return null;
        }

        return JsonSerializer.Deserialize<PluginManifest>(manifestJson, _jsonOptions);
    }

    private async Task<ExtendedManifest?> TryReadExtendedManifestAsync(string path, CancellationToken cancellationToken)
    {
        string? json = null;

        if (Directory.Exists(path))
        {
            var extendedPath = Path.Combine(path, ExtendedManifestFileName);
            if (File.Exists(extendedPath))
            {
                json = await File.ReadAllTextAsync(extendedPath, cancellationToken).ConfigureAwait(false);
            }
        }
        else if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = ZipFile.OpenRead(path);
            var entry = archive.GetEntry(ExtendedManifestFileName)
                ?? archive.GetEntry($"{Path.GetFileNameWithoutExtension(path)}/{ExtendedManifestFileName}");

            if (entry is not null)
            {
                using var stream = entry.Open();
                using var reader = new StreamReader(stream);
                json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<ExtendedManifest>(json, _jsonOptions);
    }

    private MarketplacePluginInfo CreatePluginInfo(
        PluginManifest manifest,
        ExtendedManifest? extended,
        string sourcePath)
    {
        long? packageSize = null;
        if (File.Exists(sourcePath))
        {
            packageSize = new FileInfo(sourcePath).Length;
        }
        else if (Directory.Exists(sourcePath))
        {
            packageSize = Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories)
                .Sum(file => new FileInfo(file).Length);
        }

        return new MarketplacePluginInfo
        {
            Id = manifest.Id,
            Name = manifest.Name,
            Version = manifest.Version,
            MinApiVersion = manifest.MinApiVersion,
            Category = manifest.Category ?? extended?.Category,
            Author = extended?.Author,
            Description = extended?.Description ?? manifest.Name,
            LongDescription = extended?.LongDescription,
            Tags = extended?.Tags?.ToArray() ?? Array.Empty<string>(),
            IconUrl = extended?.IconUrl,
            HomepageUrl = extended?.HomepageUrl,
            RepositoryUrl = extended?.RepositoryUrl,
            License = extended?.License,
            Screenshots = extended?.Screenshots?.ToArray() ?? Array.Empty<string>(),
            PublishedAt = extended?.PublishedAt,
            LastUpdatedAt = extended?.LastUpdatedAt,
            SourceId = SourceId,
            PackageSizeBytes = packageSize,
            AvailableVersions = new[]
            {
                new PluginVersionInfo(
                    manifest.Version,
                    manifest.MinApiVersion,
                    extended?.LastUpdatedAt,
                    extended?.ReleaseNotes,
                    packageSize)
            }
        };
    }

    private sealed record ExtendedManifest(
        string? Author,
        string? Description,
        string? LongDescription,
        string? Category,
        List<string>? Tags,
        string? IconUrl,
        string? HomepageUrl,
        string? RepositoryUrl,
        string? License,
        List<string>? Screenshots,
        string? ReleaseNotes,
        DateTimeOffset? PublishedAt,
        DateTimeOffset? LastUpdatedAt);
}
