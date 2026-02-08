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
