using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Blazor.Services.Plugins;
using NodeEditor.Blazor.Services.Registry;

namespace NodeEditor.Blazor.Tests;

public sealed class PluginLoaderTests
{
    [Fact]
    public async Task PluginLoader_LoadsSamplePlugin()
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
            options.ApiVersion = new Version(1, 0, 0);
        });

        using var provider = services.BuildServiceProvider();
        var loader = provider.GetRequiredService<PluginLoader>();
        var plugins = await loader.LoadAndRegisterAsync();

        Assert.Contains(plugins, plugin => plugin.Id == "com.nodeeditormax.sample");

        var registry = provider.GetRequiredService<NodeRegistryService>();
        var catalog = registry.GetCatalog();

        Assert.Contains(catalog.All, node => node.Name == "Multiply");
        Assert.Contains(catalog.All, node => node.Name == "Pulse");
    }

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
