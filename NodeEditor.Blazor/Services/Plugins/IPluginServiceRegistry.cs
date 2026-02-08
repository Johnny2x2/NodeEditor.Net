using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Blazor.Services.Logging;

namespace NodeEditor.Blazor.Services.Plugins;

public interface IPluginServiceRegistry
{
    IServiceProvider RegisterServices(string pluginId, Action<IServiceCollection> configureServices, INodeEditorLogger? logger = null);

    bool TryGetServices(string pluginId, out IServiceProvider services);

    bool RemoveServices(string pluginId);
}