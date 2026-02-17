namespace NodeEditor.Net.Services.Plugins.Marketplace;

/// <summary>
/// Configuration options for the plugin marketplace.
/// </summary>
public sealed class MarketplaceOptions
{
    public const long DefaultMaxUploadSizeBytes = 500L * 1024L * 1024L;

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
    /// URL of the online marketplace API.
    /// </summary>
    public string? RemoteApiUrl { get; set; }

    /// <summary>
    /// Whether to prefer online marketplace over local.
    /// </summary>
    public bool PreferOnlineMarketplace { get; set; }

    /// <summary>
    /// Cache duration for marketplace queries (in minutes).
    /// </summary>
    public int CacheDurationMinutes { get; set; } = 30;

    /// <summary>
    /// Whether to automatically check for updates on startup.
    /// </summary>
    public bool CheckForUpdatesOnStartup { get; set; } = false;

    /// <summary>
    /// Path to the cache directory.
    /// </summary>
    public string? CacheDirectory { get; set; }

    /// <summary>
    /// List of enabled marketplace sources.
    /// </summary>
    public List<string> EnabledSources { get; set; } = ["local", "remote"];

    /// <summary>
    /// Maximum number of plugins to display per page in search results.
    /// </summary>
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Whether to allow installing plugins from untrusted sources.
    /// </summary>
    public bool AllowUntrustedPlugins { get; set; } = false;

    /// <summary>
    /// Maximum accepted package upload size in bytes.
    /// </summary>
    public long MaxUploadSizeBytes { get; set; } = DefaultMaxUploadSizeBytes;
}
