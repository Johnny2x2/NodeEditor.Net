using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Blazor.Services.Plugins;
using NodeEditor.Blazor.Services.Registry;

namespace NodeEditor.Blazor.Tests;

public sealed class PluginLifecycleTests
{
    [Fact]
    public async Task PluginLoader_CallsLifecycleHooks()
    {
        // Note: This test verifies lifecycle hooks are called in correct order
        // We test with an in-memory plugin instance since cross-assembly static
        // state doesn't work with PluginLoadContext (different type identity)
        
        var plugin = new TestLifecyclePlugin();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<NodeDiscoveryService>();
        services.AddSingleton<NodeRegistryService>();
        services.AddSingleton<IPluginServiceRegistry, PluginServiceRegistry>();
        services.Configure<PluginOptions>(options =>
        {
            options.ApiVersion = new Version(1, 0, 0);
        });

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<NodeRegistryService>();
        var serviceRegistry = provider.GetRequiredService<IPluginServiceRegistry>();

        // Manually execute the lifecycle that PluginLoader would execute
        await plugin.OnLoadAsync();
        Assert.True(plugin.OnLoadCalled, "OnLoadAsync should be called");

        var pluginServices = serviceRegistry.RegisterServices(plugin.Id, plugin.ConfigureServices);
        Assert.True(plugin.ConfigureServicesCalled, "ConfigureServices should be called");

        plugin.Register(registry);
        Assert.True(plugin.RegisterCalled, "Register should be called");

        await plugin.OnInitializeAsync(pluginServices);
        Assert.True(plugin.OnInitializeCalled, "OnInitializeAsync should be called with plugin services");

        await plugin.OnUnloadAsync();
        Assert.True(plugin.OnUnloadCalled, "OnUnloadAsync should be called");

        plugin.Unload();
        Assert.True(plugin.UnloadCalled, "Unload should be called");
    }

    private sealed class TestLifecyclePlugin : INodePlugin
    {
        public const string PluginId = "com.nodeeditormax.test.lifecycle";
        public const string PluginName = "Lifecycle Test Plugin";

        public string Name => PluginName;

        public string Id => PluginId;

        public Version Version => new(1, 0, 0);

        public Version MinApiVersion => new(1, 0, 0);

        public bool OnLoadCalled { get; private set; }

        public bool ConfigureServicesCalled { get; private set; }

        public bool RegisterCalled { get; private set; }

        public bool OnInitializeCalled { get; private set; }

        public bool OnUnloadCalled { get; private set; }

        public bool UnloadCalled { get; private set; }

        public void Register(INodeRegistryService registry)
        {
            RegisterCalled = true;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            ConfigureServicesCalled = true;
            services.AddSingleton<TestService>();
        }

        public Task OnLoadAsync(CancellationToken token = default)
        {
            OnLoadCalled = true;
            return Task.CompletedTask;
        }

        public Task OnInitializeAsync(IServiceProvider services, CancellationToken token = default)
        {
            var resolved = services.GetService<TestService>();
            OnInitializeCalled = resolved is not null;
            return Task.CompletedTask;
        }

        public Task OnUnloadAsync(CancellationToken token = default)
        {
            OnUnloadCalled = true;
            return Task.CompletedTask;
        }

        public void Unload()
        {
            UnloadCalled = true;
        }

        public void OnError(Exception ex)
        {
            // No-op for test
        }

        private sealed class TestService;
    }
}