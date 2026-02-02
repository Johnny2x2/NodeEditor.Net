# Stage 1: Abstractions, Models, and Local Storage Backend

## Overview

This stage establishes the foundational interfaces, models, and a local storage implementation for the plugin marketplace. The goal is to create a clean abstraction layer that allows easy swapping between local storage (for development/offline use) and a future online marketplace API.

## Goals

- [x] Define all marketplace-related interfaces
- [x] Create data models for marketplace plugins and installation state
- [x] Implement local storage backend that reads from a local folder
- [x] Create installation service for managing plugin lifecycle
- [x] Extend existing PluginLoader with unload capability

---

## 1. Core Interfaces

### 1.1 IPluginMarketplaceSource

The main abstraction for fetching plugin information. Can be backed by local storage or a remote API.

**File:** `NodeEditor.Blazor/Services/Plugins/Marketplace/IPluginMarketplaceSource.cs`

```csharp
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
    /// <param name="query">Search text (null/empty returns all).</param>
    /// <param name="category">Optional category filter.</param>
    /// <param name="tags">Optional tag filters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching plugins.</returns>
    Task<IReadOnlyList<MarketplacePluginInfo>> SearchAsync(
        string? query = null,
        string? category = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get detailed information about a specific plugin.
    /// </summary>
    /// <param name="pluginId">The unique plugin identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Plugin details or null if not found.</returns>
    Task<MarketplacePluginInfo?> GetDetailsAsync(
        string pluginId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Download a plugin package as a stream.
    /// </summary>
    /// <param name="pluginId">The unique plugin identifier.</param>
    /// <param name="version">Specific version to download (null for latest).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Stream containing the plugin package (zip or folder contents).</returns>
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
```

### 1.2 IPluginInstallationService

Manages the lifecycle of installed plugins (install, uninstall, update, list).

**File:** `NodeEditor.Blazor/Services/Plugins/Marketplace/IPluginInstallationService.cs`

```csharp
namespace NodeEditor.Blazor.Services.Plugins.Marketplace;

/// <summary>
/// Service for managing plugin installation lifecycle.
/// </summary>
public interface IPluginInstallationService
{
    /// <summary>
    /// Fired when a plugin is installed.
    /// </summary>
    event EventHandler<PluginInstalledEventArgs>? PluginInstalled;
    
    /// <summary>
    /// Fired when a plugin is uninstalled.
    /// </summary>
    event EventHandler<PluginUninstalledEventArgs>? PluginUninstalled;
    
    /// <summary>
    /// Fired when a plugin is updated.
    /// </summary>
    event EventHandler<PluginUpdatedEventArgs>? PluginUpdated;
    
    /// <summary>
    /// Get all currently installed plugins.
    /// </summary>
    Task<IReadOnlyList<InstalledPluginInfo>> GetInstalledPluginsAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if a specific plugin is installed.
    /// </summary>
    Task<InstalledPluginInfo?> GetInstalledPluginAsync(
        string pluginId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Install a plugin from a marketplace source.
    /// </summary>
    /// <param name="source">The marketplace source to download from.</param>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="version">Specific version (null for latest).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Installation result with details.</returns>
    Task<PluginInstallResult> InstallAsync(
        IPluginMarketplaceSource source,
        string pluginId,
        string? version = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Install a plugin from a local package file.
    /// </summary>
    /// <param name="packagePath">Path to the plugin package (zip file or folder).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PluginInstallResult> InstallFromPackageAsync(
        string packagePath,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Uninstall a plugin.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PluginUninstallResult> UninstallAsync(
        string pluginId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update a plugin to the latest version.
    /// </summary>
    /// <param name="source">The marketplace source.</param>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="targetVersion">Specific version (null for latest).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PluginInstallResult> UpdateAsync(
        IPluginMarketplaceSource source,
        string pluginId,
        string? targetVersion = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check for available updates for all installed plugins.
    /// </summary>
    Task<IReadOnlyList<PluginUpdateInfo>> CheckForUpdatesAsync(
        IPluginMarketplaceSource source,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Enable a disabled plugin.
    /// </summary>
    Task<bool> EnablePluginAsync(string pluginId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Disable a plugin without uninstalling.
    /// </summary>
    Task<bool> DisablePluginAsync(string pluginId, CancellationToken cancellationToken = default);
}
```

### 1.3 IPluginMarketplaceAuthProvider (Future-Proofing)

Placeholder interface for future online marketplace authentication.

**File:** `NodeEditor.Blazor/Services/Plugins/Marketplace/IPluginMarketplaceAuthProvider.cs`

```csharp
namespace NodeEditor.Blazor.Services.Plugins.Marketplace;

/// <summary>
/// Abstraction for marketplace authentication.
/// Placeholder for future online marketplace integration.
/// </summary>
public interface IPluginMarketplaceAuthProvider
{
    /// <summary>
    /// Whether the user is currently authenticated.
    /// </summary>
    bool IsAuthenticated { get; }
    
    /// <summary>
    /// Current user info (null if not authenticated).
    /// </summary>
    MarketplaceUserInfo? CurrentUser { get; }
    
    /// <summary>
    /// Sign in to the marketplace.
    /// </summary>
    Task<AuthResult> SignInAsync(string username, string password, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sign out from the marketplace.
    /// </summary>
    Task SignOutAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get authentication headers for API requests.
    /// </summary>
    Task<IDictionary<string, string>> GetAuthHeadersAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Basic user info from marketplace.
/// </summary>
public sealed record MarketplaceUserInfo(
    string UserId,
    string Username,
    string? Email,
    string? AvatarUrl);

/// <summary>
/// Authentication result.
/// </summary>
public sealed record AuthResult(
    bool Success,
    string? ErrorMessage = null,
    MarketplaceUserInfo? User = null);
```

---

## 2. Data Models

### 2.1 MarketplacePluginInfo

Extended plugin metadata for marketplace display.

**File:** `NodeEditor.Blazor/Services/Plugins/Marketplace/Models/MarketplacePluginInfo.cs`

```csharp
namespace NodeEditor.Blazor.Services.Plugins.Marketplace.Models;

/// <summary>
/// Complete plugin information from the marketplace.
/// Extends the basic PluginManifest with marketplace-specific metadata.
/// </summary>
public sealed record MarketplacePluginInfo
{
    /// <summary>
    /// Unique plugin identifier (e.g., "com.nodeeditormax.sample").
    /// </summary>
    public required string Id { get; init; }
    
    /// <summary>
    /// Display name.
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Latest available version.
    /// </summary>
    public required string Version { get; init; }
    
    /// <summary>
    /// Minimum API version required.
    /// </summary>
    public required string MinApiVersion { get; init; }
    
    /// <summary>
    /// Plugin author/publisher name.
    /// </summary>
    public string? Author { get; init; }
    
    /// <summary>
    /// Short description.
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// Long-form description (can contain markdown).
    /// </summary>
    public string? LongDescription { get; init; }
    
    /// <summary>
    /// Category for grouping (e.g., "Math", "AI", "Image Processing").
    /// </summary>
    public string? Category { get; init; }
    
    /// <summary>
    /// Searchable tags.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
    
    /// <summary>
    /// URL to plugin icon (can be local path or HTTP URL).
    /// </summary>
    public string? IconUrl { get; init; }
    
    /// <summary>
    /// URL to plugin homepage/documentation.
    /// </summary>
    public string? HomepageUrl { get; init; }
    
    /// <summary>
    /// URL to source repository (GitHub, etc.).
    /// </summary>
    public string? RepositoryUrl { get; init; }
    
    /// <summary>
    /// License identifier (e.g., "MIT", "Apache-2.0").
    /// </summary>
    public string? License { get; init; }
    
    /// <summary>
    /// All available versions (newest first).
    /// </summary>
    public IReadOnlyList<PluginVersionInfo> AvailableVersions { get; init; } = [];
    
    /// <summary>
    /// Screenshots or preview images.
    /// </summary>
    public IReadOnlyList<string> Screenshots { get; init; } = [];
    
    /// <summary>
    /// Download count (for popularity display).
    /// </summary>
    public int DownloadCount { get; init; }
    
    /// <summary>
    /// Average rating (0-5).
    /// </summary>
    public double? Rating { get; init; }
    
    /// <summary>
    /// Number of ratings.
    /// </summary>
    public int RatingCount { get; init; }
    
    /// <summary>
    /// When the plugin was first published.
    /// </summary>
    public DateTimeOffset? PublishedAt { get; init; }
    
    /// <summary>
    /// When the latest version was released.
    /// </summary>
    public DateTimeOffset? LastUpdatedAt { get; init; }
    
    /// <summary>
    /// Source identifier (which marketplace this came from).
    /// </summary>
    public string? SourceId { get; init; }
    
    /// <summary>
    /// Size of the plugin package in bytes.
    /// </summary>
    public long? PackageSizeBytes { get; init; }
}

/// <summary>
/// Information about a specific plugin version.
/// </summary>
public sealed record PluginVersionInfo(
    string Version,
    string MinApiVersion,
    DateTimeOffset? ReleasedAt,
    string? ReleaseNotes,
    long? PackageSizeBytes);
```

### 2.2 InstalledPluginInfo

Tracks installation state of plugins.

**File:** `NodeEditor.Blazor/Services/Plugins/Marketplace/Models/InstalledPluginInfo.cs`

```csharp
namespace NodeEditor.Blazor.Services.Plugins.Marketplace.Models;

/// <summary>
/// Information about an installed plugin.
/// </summary>
public sealed record InstalledPluginInfo
{
    /// <summary>
    /// Unique plugin identifier.
    /// </summary>
    public required string Id { get; init; }
    
    /// <summary>
    /// Display name.
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Installed version.
    /// </summary>
    public required string Version { get; init; }
    
    /// <summary>
    /// When the plugin was installed.
    /// </summary>
    public DateTimeOffset InstalledAt { get; init; }
    
    /// <summary>
    /// Which marketplace source it was installed from.
    /// </summary>
    public string? SourceId { get; init; }
    
    /// <summary>
    /// Path to the installed plugin directory.
    /// </summary>
    public required string InstallPath { get; init; }
    
    /// <summary>
    /// Whether the plugin is currently enabled.
    /// </summary>
    public bool IsEnabled { get; init; } = true;
    
    /// <summary>
    /// Whether the plugin loaded successfully.
    /// </summary>
    public bool IsLoaded { get; init; }
    
    /// <summary>
    /// Error message if loading failed.
    /// </summary>
    public string? LoadError { get; init; }
    
    /// <summary>
    /// Author name (cached from marketplace).
    /// </summary>
    public string? Author { get; init; }
    
    /// <summary>
    /// Description (cached from marketplace).
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// Category (cached from marketplace).
    /// </summary>
    public string? Category { get; init; }
}
```

### 2.3 Result Types

**File:** `NodeEditor.Blazor/Services/Plugins/Marketplace/Models/PluginOperationResults.cs`

```csharp
namespace NodeEditor.Blazor.Services.Plugins.Marketplace.Models;

/// <summary>
/// Result of a plugin download operation.
/// </summary>
public sealed record PluginDownloadResult(
    bool Success,
    Stream? PackageStream = null,
    string? LocalPath = null,
    string? ErrorMessage = null,
    string? Version = null);

/// <summary>
/// Result of a plugin installation operation.
/// </summary>
public sealed record PluginInstallResult(
    bool Success,
    InstalledPluginInfo? Plugin = null,
    string? ErrorMessage = null,
    PluginInstallErrorCode ErrorCode = PluginInstallErrorCode.None);

/// <summary>
/// Error codes for installation failures.
/// </summary>
public enum PluginInstallErrorCode
{
    None = 0,
    PluginNotFound,
    VersionNotFound,
    IncompatibleApiVersion,
    DownloadFailed,
    ExtractionFailed,
    ManifestInvalid,
    AlreadyInstalled,
    DependencyMissing,
    PermissionDenied,
    DiskFull,
    Unknown
}

/// <summary>
/// Result of a plugin uninstall operation.
/// </summary>
public sealed record PluginUninstallResult(
    bool Success,
    string? ErrorMessage = null);

/// <summary>
/// Information about an available update.
/// </summary>
public sealed record PluginUpdateInfo(
    string PluginId,
    string PluginName,
    string CurrentVersion,
    string AvailableVersion,
    string? ReleaseNotes);
```

### 2.4 Event Args

**File:** `NodeEditor.Blazor/Services/Plugins/Marketplace/Models/PluginEventArgs.cs`

```csharp
namespace NodeEditor.Blazor.Services.Plugins.Marketplace.Models;

/// <summary>
/// Event args for plugin installed event.
/// </summary>
public sealed class PluginInstalledEventArgs : EventArgs
{
    public required InstalledPluginInfo Plugin { get; init; }
}

/// <summary>
/// Event args for plugin uninstalled event.
/// </summary>
public sealed class PluginUninstalledEventArgs : EventArgs
{
    public required string PluginId { get; init; }
    public required string PluginName { get; init; }
}

/// <summary>
/// Event args for plugin updated event.
/// </summary>
public sealed class PluginUpdatedEventArgs : EventArgs
{
    public required InstalledPluginInfo Plugin { get; init; }
    public required string PreviousVersion { get; init; }
}
```

---

## 3. Local Storage Implementation

### 3.1 LocalPluginMarketplaceSource

Reads available plugins from a local "repository" folder.

**File:** `NodeEditor.Blazor/Services/Plugins/Marketplace/LocalPluginMarketplaceSource.cs`

```csharp
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodeEditor.Blazor.Services.Plugins.Marketplace.Models;

namespace NodeEditor.Blazor.Services.Plugins.Marketplace;

/// <summary>
/// Local file system-based plugin marketplace source.
/// Scans a local folder for plugin packages (folders with plugin.json or .zip files).
/// </summary>
public sealed class LocalPluginMarketplaceSource : IPluginMarketplaceSource
{
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
        var plugins = await ScanRepositoryAsync(cancellationToken);
        
        // Apply filters
        var filtered = plugins.AsEnumerable();
        
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim().ToLowerInvariant();
            filtered = filtered.Where(p =>
                p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.Id.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (p.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.Author?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                p.Tags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase)));
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
        
        return filtered.OrderBy(p => p.Name).ToList();
    }
    
    public async Task<MarketplacePluginInfo?> GetDetailsAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        var plugins = await ScanRepositoryAsync(cancellationToken);
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
        
        // Look for plugin folder or zip
        foreach (var entry in Directory.EnumerateFileSystemEntries(repoPath))
        {
            var manifest = await TryReadManifestAsync(entry, cancellationToken);
            if (manifest is null) continue;
            
            if (!string.Equals(manifest.Id, pluginId, StringComparison.OrdinalIgnoreCase))
                continue;
            
            // Found the plugin - return path for local installation
            return new PluginDownloadResult(
                Success: true,
                LocalPath: entry,
                Version: manifest.Version);
        }
        
        return new PluginDownloadResult(false, ErrorMessage: $"Plugin '{pluginId}' not found in repository.");
    }
    
    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var plugins = await ScanRepositoryAsync(cancellationToken);
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
            return _options.LocalRepositoryPath;
        
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
            try
            {
                var manifest = await TryReadManifestAsync(entry, cancellationToken);
                if (manifest is null) continue;
                
                var extendedManifest = await TryReadExtendedManifestAsync(entry, cancellationToken);
                
                var plugin = CreatePluginInfo(manifest, extendedManifest, entry);
                plugins.Add(plugin);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read plugin from {Entry}", entry);
            }
        }
        
        return plugins;
    }
    
    private async Task<LocalManifest?> TryReadManifestAsync(string path, CancellationToken cancellationToken)
    {
        string? manifestJson = null;
        
        if (Directory.Exists(path))
        {
            // It's a folder - look for plugin.json
            var manifestPath = Path.Combine(path, _options.ManifestFileName);
            if (File.Exists(manifestPath))
            {
                manifestJson = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            }
        }
        else if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            // It's a zip - extract plugin.json
            using var archive = ZipFile.OpenRead(path);
            var manifestEntry = archive.GetEntry(_options.ManifestFileName)
                ?? archive.GetEntry($"{Path.GetFileNameWithoutExtension(path)}/{_options.ManifestFileName}");
            
            if (manifestEntry is not null)
            {
                using var stream = manifestEntry.Open();
                using var reader = new StreamReader(stream);
                manifestJson = await reader.ReadToEndAsync(cancellationToken);
            }
        }
        
        if (string.IsNullOrWhiteSpace(manifestJson))
            return null;
        
        return JsonSerializer.Deserialize<LocalManifest>(manifestJson, _jsonOptions);
    }
    
    private async Task<ExtendedManifest?> TryReadExtendedManifestAsync(string path, CancellationToken cancellationToken)
    {
        string? json = null;
        const string extendedFileName = "plugin-marketplace.json";
        
        if (Directory.Exists(path))
        {
            var extendedPath = Path.Combine(path, extendedFileName);
            if (File.Exists(extendedPath))
            {
                json = await File.ReadAllTextAsync(extendedPath, cancellationToken);
            }
        }
        else if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = ZipFile.OpenRead(path);
            var entry = archive.GetEntry(extendedFileName)
                ?? archive.GetEntry($"{Path.GetFileNameWithoutExtension(path)}/{extendedFileName}");
            
            if (entry is not null)
            {
                using var stream = entry.Open();
                using var reader = new StreamReader(stream);
                json = await reader.ReadToEndAsync(cancellationToken);
            }
        }
        
        if (string.IsNullOrWhiteSpace(json))
            return null;
        
        return JsonSerializer.Deserialize<ExtendedManifest>(json, _jsonOptions);
    }
    
    private MarketplacePluginInfo CreatePluginInfo(
        LocalManifest manifest,
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
                .Sum(f => new FileInfo(f).Length);
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
            Tags = extended?.Tags ?? [],
            IconUrl = extended?.IconUrl,
            HomepageUrl = extended?.HomepageUrl,
            RepositoryUrl = extended?.RepositoryUrl,
            License = extended?.License,
            Screenshots = extended?.Screenshots ?? [],
            PublishedAt = extended?.PublishedAt,
            LastUpdatedAt = extended?.LastUpdatedAt,
            SourceId = SourceId,
            PackageSizeBytes = packageSize,
            AvailableVersions =
            [
                new PluginVersionInfo(
                    manifest.Version,
                    manifest.MinApiVersion,
                    extended?.LastUpdatedAt,
                    extended?.ReleaseNotes,
                    packageSize)
            ]
        };
    }
    
    // Internal manifest models for JSON deserialization
    private sealed record LocalManifest(
        string Id,
        string Name,
        string Version,
        string MinApiVersion,
        string? EntryAssembly,
        string? Category);
    
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
```

### 3.2 PluginInstallationService

**File:** `NodeEditor.Blazor/Services/Plugins/Marketplace/PluginInstallationService.cs`

```csharp
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodeEditor.Blazor.Services.Plugins.Marketplace.Models;

namespace NodeEditor.Blazor.Services.Plugins.Marketplace;

/// <summary>
/// Manages plugin installation, uninstallation, and state tracking.
/// </summary>
public sealed class PluginInstallationService : IPluginInstallationService
{
    private readonly PluginOptions _pluginOptions;
    private readonly MarketplaceOptions _marketplaceOptions;
    private readonly PluginLoader _pluginLoader;
    private readonly ILogger<PluginInstallationService> _logger;
    
    private const string InstalledManifestFileName = "installed-plugins.json";
    
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
        var manifest = await LoadInstalledManifestAsync(cancellationToken);
        return manifest.Plugins;
    }
    
    public async Task<InstalledPluginInfo?> GetInstalledPluginAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        var plugins = await GetInstalledPluginsAsync(cancellationToken);
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
        
        // Check if already installed
        var existing = await GetInstalledPluginAsync(pluginId, cancellationToken);
        if (existing is not null)
        {
            return new PluginInstallResult(
                false,
                ErrorMessage: $"Plugin '{pluginId}' is already installed (version {existing.Version}).",
                ErrorCode: PluginInstallErrorCode.AlreadyInstalled);
        }
        
        // Download
        var downloadResult = await source.DownloadAsync(pluginId, version, cancellationToken);
        if (!downloadResult.Success)
        {
            return new PluginInstallResult(
                false,
                ErrorMessage: downloadResult.ErrorMessage ?? "Download failed.",
                ErrorCode: PluginInstallErrorCode.DownloadFailed);
        }
        
        // Install from the downloaded package
        return await InstallFromPackageInternalAsync(
            downloadResult.LocalPath ?? throw new InvalidOperationException("No local path"),
            downloadResult.PackageStream,
            source.SourceId,
            cancellationToken);
    }
    
    public Task<PluginInstallResult> InstallFromPackageAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        return InstallFromPackageInternalAsync(packagePath, null, null, cancellationToken);
    }
    
    private async Task<PluginInstallResult> InstallFromPackageInternalAsync(
        string packagePath,
        Stream? packageStream,
        string? sourceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var pluginsDir = GetPluginsDirectory();
            Directory.CreateDirectory(pluginsDir);
            
            string targetDir;
            
            if (packagePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                // Extract zip
                var tempDir = Path.Combine(Path.GetTempPath(), $"plugin-{Guid.NewGuid():N}");
                if (packageStream is not null)
                {
                    using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read);
                    archive.ExtractToDirectory(tempDir);
                }
                else
                {
                    ZipFile.ExtractToDirectory(packagePath, tempDir);
                }
                
                // Find the plugin folder (might be nested)
                var manifestPath = FindManifestRecursive(tempDir);
                if (manifestPath is null)
                {
                    Directory.Delete(tempDir, true);
                    return new PluginInstallResult(
                        false,
                        ErrorMessage: "No plugin.json found in package.",
                        ErrorCode: PluginInstallErrorCode.ManifestInvalid);
                }
                
                var pluginSourceDir = Path.GetDirectoryName(manifestPath)!;
                var manifest = await ReadManifestAsync(manifestPath, cancellationToken);
                
                targetDir = Path.Combine(pluginsDir, SanitizeDirectoryName(manifest.Id));
                
                if (Directory.Exists(targetDir))
                    Directory.Delete(targetDir, true);
                
                Directory.Move(pluginSourceDir, targetDir);
                
                // Clean up temp dir if it still exists
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
            else if (Directory.Exists(packagePath))
            {
                // Copy folder
                var manifestPath = Path.Combine(packagePath, _marketplaceOptions.ManifestFileName);
                if (!File.Exists(manifestPath))
                {
                    return new PluginInstallResult(
                        false,
                        ErrorMessage: "No plugin.json found in folder.",
                        ErrorCode: PluginInstallErrorCode.ManifestInvalid);
                }
                
                var manifest = await ReadManifestAsync(manifestPath, cancellationToken);
                targetDir = Path.Combine(pluginsDir, SanitizeDirectoryName(manifest.Id));
                
                if (Directory.Exists(targetDir))
                    Directory.Delete(targetDir, true);
                
                CopyDirectory(packagePath, targetDir);
            }
            else
            {
                return new PluginInstallResult(
                    false,
                    ErrorMessage: $"Package path does not exist: {packagePath}",
                    ErrorCode: PluginInstallErrorCode.DownloadFailed);
            }
            
            // Read the installed manifest
            var installedManifestPath = Path.Combine(targetDir, _marketplaceOptions.ManifestFileName);
            var installedManifest = await ReadManifestAsync(installedManifestPath, cancellationToken);
            
            // Create installed plugin info
            var installedPlugin = new InstalledPluginInfo
            {
                Id = installedManifest.Id,
                Name = installedManifest.Name,
                Version = installedManifest.Version,
                InstallPath = targetDir,
                InstalledAt = DateTimeOffset.UtcNow,
                SourceId = sourceId,
                IsEnabled = true,
                Category = installedManifest.Category
            };
            
            // Update installed manifest
            var manifestData = await LoadInstalledManifestAsync(cancellationToken);
            manifestData.Plugins.Add(installedPlugin);
            await SaveInstalledManifestAsync(manifestData, cancellationToken);
            
            // Reload plugins
            await _pluginLoader.LoadAndRegisterAsync();
            
            // Update with load status
            installedPlugin = installedPlugin with { IsLoaded = true };
            
            _logger.LogInformation("Successfully installed plugin {PluginId} v{Version}", 
                installedPlugin.Id, installedPlugin.Version);
            
            PluginInstalled?.Invoke(this, new PluginInstalledEventArgs { Plugin = installedPlugin });
            
            return new PluginInstallResult(true, installedPlugin);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install plugin from {Path}", packagePath);
            return new PluginInstallResult(
                false,
                ErrorMessage: ex.Message,
                ErrorCode: PluginInstallErrorCode.Unknown);
        }
    }
    
    public async Task<PluginUninstallResult> UninstallAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Uninstalling plugin {PluginId}", pluginId);
        
        var installed = await GetInstalledPluginAsync(pluginId, cancellationToken);
        if (installed is null)
        {
            return new PluginUninstallResult(false, $"Plugin '{pluginId}' is not installed.");
        }
        
        try
        {
            // Unload the plugin first
            await _pluginLoader.UnloadPluginAsync(pluginId);
            
            // Delete the plugin folder
            if (Directory.Exists(installed.InstallPath))
            {
                Directory.Delete(installed.InstallPath, true);
            }
            
            // Update manifest
            var manifest = await LoadInstalledManifestAsync(cancellationToken);
            manifest.Plugins.RemoveAll(p =>
                string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));
            await SaveInstalledManifestAsync(manifest, cancellationToken);
            
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
        var installed = await GetInstalledPluginAsync(pluginId, cancellationToken);
        if (installed is null)
        {
            return new PluginInstallResult(
                false,
                ErrorMessage: $"Plugin '{pluginId}' is not installed.",
                ErrorCode: PluginInstallErrorCode.PluginNotFound);
        }
        
        var previousVersion = installed.Version;
        
        // Uninstall old version
        var uninstallResult = await UninstallAsync(pluginId, cancellationToken);
        if (!uninstallResult.Success)
        {
            return new PluginInstallResult(
                false,
                ErrorMessage: uninstallResult.ErrorMessage,
                ErrorCode: PluginInstallErrorCode.Unknown);
        }
        
        // Install new version
        var installResult = await InstallAsync(source, pluginId, targetVersion, cancellationToken);
        
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
        var installed = await GetInstalledPluginsAsync(cancellationToken);
        var updates = new List<PluginUpdateInfo>();
        
        foreach (var plugin in installed)
        {
            var marketplaceInfo = await source.GetDetailsAsync(plugin.Id, cancellationToken);
            if (marketplaceInfo is null) continue;
            
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
    
    public async Task<bool> EnablePluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        return await SetPluginEnabledAsync(pluginId, true, cancellationToken);
    }
    
    public async Task<bool> DisablePluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        return await SetPluginEnabledAsync(pluginId, false, cancellationToken);
    }
    
    private async Task<bool> SetPluginEnabledAsync(string pluginId, bool enabled, CancellationToken cancellationToken)
    {
        var manifest = await LoadInstalledManifestAsync(cancellationToken);
        var plugin = manifest.Plugins.FirstOrDefault(p =>
            string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));
        
        if (plugin is null) return false;
        
        var index = manifest.Plugins.IndexOf(plugin);
        manifest.Plugins[index] = plugin with { IsEnabled = enabled };
        
        await SaveInstalledManifestAsync(manifest, cancellationToken);
        
        // Reload plugins
        await _pluginLoader.LoadAndRegisterAsync();
        
        return true;
    }
    
    private string GetPluginsDirectory()
    {
        if (Path.IsPathRooted(_pluginOptions.PluginDirectory))
            return _pluginOptions.PluginDirectory;
        
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
            return new InstalledPluginsManifest();
        
        var json = await File.ReadAllTextAsync(path, cancellationToken);
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
        
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }
    
    private static string? FindManifestRecursive(string directory)
    {
        var manifestPath = Path.Combine(directory, "plugin.json");
        if (File.Exists(manifestPath))
            return manifestPath;
        
        foreach (var subDir in Directory.EnumerateDirectories(directory))
        {
            var found = FindManifestRecursive(subDir);
            if (found is not null)
                return found;
        }
        
        return null;
    }
    
    private static async Task<PluginManifestData> ReadManifestAsync(string path, CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<PluginManifestData>(json, new JsonSerializerOptions
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
    
    private sealed record PluginManifestData(
        string Id,
        string Name,
        string Version,
        string MinApiVersion,
        string? Category);
    
    private sealed class InstalledPluginsManifest
    {
        public int Version { get; set; } = 1;
        public List<InstalledPluginInfo> Plugins { get; set; } = [];
    }
}
```

### 3.3 MarketplaceOptions

**File:** `NodeEditor.Blazor/Services/Plugins/Marketplace/MarketplaceOptions.cs`

```csharp
namespace NodeEditor.Blazor.Services.Plugins.Marketplace;

/// <summary>
/// Configuration options for the plugin marketplace.
/// </summary>
public sealed class MarketplaceOptions
{
    /// <summary>
    /// Path to the local plugin repository folder.
    /// Can be absolute or relative to application base directory.
    /// </summary>
    public string LocalRepositoryPath { get; set; } = "plugin-repository";
    
    /// <summary>
    /// Manifest file name to look for in plugins.
    /// </summary>
    public string ManifestFileName { get; set; } = "plugin.json";
    
    /// <summary>
    /// Future: URL of the online marketplace API.
    /// </summary>
    public string? RemoteApiUrl { get; set; }
    
    /// <summary>
    /// Future: Whether to prefer online marketplace over local.
    /// </summary>
    public bool PreferOnlineMarketplace { get; set; } = false;
    
    /// <summary>
    /// Cache duration for marketplace queries (in minutes).
    /// </summary>
    public int CacheDurationMinutes { get; set; } = 30;
    
    /// <summary>
    /// Whether to automatically check for updates on startup.
    /// </summary>
    public bool CheckForUpdatesOnStartup { get; set; } = false;
}
```

---

## 4. PluginLoader Extensions

Add unload capability to the existing PluginLoader.

**Extensions to:** `NodeEditor.Blazor/Services/Plugins/PluginLoader.cs`

```csharp
// Add these methods to the existing PluginLoader class:

private readonly Dictionary<string, (INodePlugin Plugin, PluginLoadContext Context)> _loadedPlugins = new();

/// <summary>
/// Unload a specific plugin by ID.
/// </summary>
public Task UnloadPluginAsync(string pluginId)
{
    if (_loadedPlugins.TryGetValue(pluginId, out var entry))
    {
        entry.Plugin.Unload();
        entry.Context.Unload();
        _loadedPlugins.Remove(pluginId);
        
        _logger.LogInformation("Unloaded plugin {PluginId}", pluginId);
    }
    
    return Task.CompletedTask;
}

/// <summary>
/// Unload all plugins.
/// </summary>
public Task UnloadAllPluginsAsync()
{
    foreach (var (pluginId, entry) in _loadedPlugins)
    {
        try
        {
            entry.Plugin.Unload();
            entry.Context.Unload();
            _logger.LogInformation("Unloaded plugin {PluginId}", pluginId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to unload plugin {PluginId}", pluginId);
        }
    }
    
    _loadedPlugins.Clear();
    return Task.CompletedTask;
}

// Modify the existing LoadCandidate method to track loaded plugins:
// After successfully loading a plugin, add:
// _loadedPlugins[plugin.Id] = (plugin, loadContext);
```

---

## 5. Service Registration

**Update:** `NodeEditor.Blazor/Services/ServiceCollectionExtensions.cs`

```csharp
using NodeEditor.Blazor.Services.Plugins.Marketplace;

// Add to the AddNodeEditor method:
public static IServiceCollection AddNodeEditor(this IServiceCollection services)
{
    // ... existing registrations ...
    
    // Marketplace services
    services.AddOptions<MarketplaceOptions>();
    services.AddSingleton<IPluginMarketplaceSource, LocalPluginMarketplaceSource>();
    services.AddSingleton<IPluginInstallationService, PluginInstallationService>();
    
    return services;
}
```

---

## 6. Extended Plugin Manifest Format

For local repository plugins, create a `plugin-marketplace.json` alongside `plugin.json`:

```json
{
  "author": "NodeEditorMax Team",
  "description": "A sample plugin demonstrating basic math and string operations.",
  "longDescription": "# Sample Plugin\n\nThis plugin provides...",
  "category": "Utilities",
  "tags": ["math", "sample", "getting-started"],
  "iconUrl": "icon.png",
  "homepageUrl": "https://github.com/Johnny2x2/NodeEditorMax",
  "repositoryUrl": "https://github.com/Johnny2x2/NodeEditorMax",
  "license": "MIT",
  "screenshots": ["screenshot1.png", "screenshot2.png"],
  "releaseNotes": "Initial release with basic nodes.",
  "publishedAt": "2026-01-15T00:00:00Z",
  "lastUpdatedAt": "2026-02-01T00:00:00Z"
}
```

---

## Deliverables Checklist

| File | Status |
|------|--------|
| `Services/Plugins/Marketplace/IPluginMarketplaceSource.cs` | To create |
| `Services/Plugins/Marketplace/IPluginInstallationService.cs` | To create |
| `Services/Plugins/Marketplace/IPluginMarketplaceAuthProvider.cs` | To create |
| `Services/Plugins/Marketplace/Models/MarketplacePluginInfo.cs` | To create |
| `Services/Plugins/Marketplace/Models/InstalledPluginInfo.cs` | To create |
| `Services/Plugins/Marketplace/Models/PluginOperationResults.cs` | To create |
| `Services/Plugins/Marketplace/Models/PluginEventArgs.cs` | To create |
| `Services/Plugins/Marketplace/LocalPluginMarketplaceSource.cs` | To create |
| `Services/Plugins/Marketplace/PluginInstallationService.cs` | To create |
| `Services/Plugins/Marketplace/MarketplaceOptions.cs` | To create |
| `Services/Plugins/PluginLoader.cs` | To modify (add unload) |
| `Services/ServiceCollectionExtensions.cs` | To modify (add registrations) |

---

## Testing Strategy

1. **Unit Tests:**
   - `LocalPluginMarketplaceSourceTests.cs` - Test scanning, filtering, download
   - `PluginInstallationServiceTests.cs` - Test install/uninstall/update flows

2. **Integration Tests:**
   - Create test repository folder with sample plugins
   - Verify full install → load → uninstall cycle

---

## Next Stage Preview

**Stage 2** will implement the UI components:
- `PluginManagerDialog.razor` - Main marketplace window
- `PluginCard.razor` - Plugin display cards
- Search and filtering UI
- Install/uninstall buttons with progress feedback
