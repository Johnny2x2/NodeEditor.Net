namespace NodeEditor.Net.Services.Plugins;

public interface IPluginLoader
{
    Task<IReadOnlyList<INodePlugin>> LoadAndRegisterAsync(
        string? pluginDirectory = null,
        IServiceProvider? services = null,
        CancellationToken token = default);

    Task<IReadOnlyList<INodePlugin>> LoadPluginsAsync(
        string? pluginDirectory = null,
        CancellationToken token = default);

    Task UnloadPluginAsync(string pluginId, CancellationToken token = default);
    Task UnloadAllPluginsAsync(CancellationToken token = default);

    /// <summary>
    /// Returns the plugin ID and name for the plugin that provides the given definition ID,
    /// or null if the definition is not from a plugin (i.e. built-in).
    /// </summary>
    (string PluginId, string PluginName, string? Version)? GetPluginForDefinition(string definitionId);

    /// <summary>
    /// Returns the set of currently loaded plugins as (Id, Name, Version) tuples.
    /// </summary>
    IReadOnlyList<(string PluginId, string PluginName, string? Version)> GetLoadedPlugins();
}
