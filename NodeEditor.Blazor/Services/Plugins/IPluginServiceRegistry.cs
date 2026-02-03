using Microsoft.Extensions.DependencyInjection;

namespace NodeEditor.Blazor.Services.Plugins;

public interface IPluginServiceRegistry
{
    IServiceProvider RegisterServices(string pluginId, Action<IServiceCollection> configureServices);

    bool TryGetServices(string pluginId, out IServiceProvider services);

    bool RemoveServices(string pluginId);
}