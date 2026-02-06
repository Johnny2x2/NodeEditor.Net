using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Blazor.Services.Plugins;
using NodeEditor.Blazor.Services.Registry;

namespace NodeEditor.Blazor.Tests;

public sealed class PluginLoaderTests
{


    [Fact]
    public async Task PluginLoader_SkipsWhenDisabled()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<NodeDiscoveryService>();
        services.AddSingleton<NodeRegistryService>();
        services.AddSingleton<IPluginServiceRegistry, PluginServiceRegistry>();
        services.AddSingleton<PluginLoader>();
        services.Configure<PluginOptions>(options =>
        {
            options.PluginDirectory = Path.Combine(AppContext.BaseDirectory, "plugins");
            options.EnablePluginLoading = false;
        });

        using var provider = services.BuildServiceProvider();
        var loader = provider.GetRequiredService<PluginLoader>();
        var plugins = await loader.LoadPluginsAsync();

        Assert.Empty(plugins);
    }
}
