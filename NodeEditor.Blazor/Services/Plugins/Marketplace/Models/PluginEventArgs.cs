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
