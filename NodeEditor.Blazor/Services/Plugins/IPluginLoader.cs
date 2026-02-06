namespace NodeEditor.Blazor.Services.Plugins;

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
}
