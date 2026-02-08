using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Blazor.Services.Plugins;

namespace NodeEditor.Blazor.Tests;

public sealed class PluginServiceRegistryTests
{
    [Fact]
    public void PluginServiceRegistry_RegistersAndResolvesServices()
    {
        var registry = new PluginServiceRegistry();

        var provider = registry.RegisterServices("plugin-a", services =>
        {
            services.AddSingleton<TestService>();
        });

        var resolved = provider.GetService<TestService>();

        Assert.NotNull(resolved);
    }

    [Fact]
    public void PluginServiceRegistry_RemovesServices()
    {
        var registry = new PluginServiceRegistry();

        registry.RegisterServices("plugin-b", services =>
        {
            services.AddSingleton<TestService>();
        });

        var removed = registry.RemoveServices("plugin-b");

        Assert.True(removed);
        Assert.False(registry.TryGetServices("plugin-b", out _));
    }

    private sealed class TestService;
}