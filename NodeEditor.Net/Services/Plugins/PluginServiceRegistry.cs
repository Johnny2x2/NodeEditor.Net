using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Net.Services.Logging;

namespace NodeEditor.Net.Services.Plugins;

public sealed class PluginServiceRegistry : IPluginServiceRegistry, IDisposable
{
    private readonly Dictionary<string, ServiceProvider> _providers = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public IServiceProvider RegisterServices(string pluginId, Action<IServiceCollection> configureServices, INodeEditorLogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            throw new ArgumentException("Plugin Id is required.", nameof(pluginId));
        }

        if (configureServices is null)
        {
            throw new ArgumentNullException(nameof(configureServices));
        }

        lock (_lock)
        {
            if (_providers.TryGetValue(pluginId, out var existing))
            {
                existing.Dispose();
                _providers.Remove(pluginId);
            }

            var services = new ServiceCollection();

            // Pre-seed host services so plugins can resolve them
            if (logger is not null)
            {
                services.AddSingleton(logger);
            }

            configureServices(services);
            var provider = services.BuildServiceProvider();
            _providers[pluginId] = provider;
            return provider;
        }
    }

    public bool TryGetServices(string pluginId, out IServiceProvider services)
    {
        lock (_lock)
        {
            if (_providers.TryGetValue(pluginId, out var provider))
            {
                services = provider;
                return true;
            }
        }

        services = null!;
        return false;
    }

    public bool RemoveServices(string pluginId)
    {
        lock (_lock)
        {
            if (!_providers.TryGetValue(pluginId, out var provider))
            {
                return false;
            }

            provider.Dispose();
            _providers.Remove(pluginId);
            return true;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var provider in _providers.Values)
            {
                provider.Dispose();
            }

            _providers.Clear();
        }
    }
}