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
        TestLifecyclePlugin.Reset();

        var tempDir = Directory.CreateTempSubdirectory("nodeeditor-plugin-tests");
        try
        {
            // Copy the test assembly to the temp directory so the plugin loader can find it
            var testAssemblyPath = typeof(TestLifecyclePlugin).Assembly.Location;
            var testAssemblyName = Path.GetFileName(testAssemblyPath);
            var targetAssemblyPath = Path.Combine(tempDir.FullName, testAssemblyName);
            File.Copy(testAssemblyPath, targetAssemblyPath);

            // Also copy dependencies that the test assembly needs
            var sourceDir = Path.GetDirectoryName(testAssemblyPath)!;
            foreach (var dll in Directory.GetFiles(sourceDir, "*.dll"))
            {
                var targetDll = Path.Combine(tempDir.FullName, Path.GetFileName(dll));
                if (!File.Exists(targetDll))
                {
                    try { File.Copy(dll, targetDll); } catch { }
                }
            }

            var manifest = new PluginManifest(
                TestLifecyclePlugin.PluginId,
                TestLifecyclePlugin.PluginName,
                "1.0.0",
                "1.0.0",
                testAssemblyName, // Use relative filename, not absolute path
                null);

            var manifestPath = Path.Combine(tempDir.FullName, "plugin.json");
            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(manifestPath, manifestJson);

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<NodeDiscoveryService>();
            services.AddSingleton<NodeRegistryService>();
            services.AddSingleton<IPluginServiceRegistry, PluginServiceRegistry>();
            services.AddSingleton<PluginLoader>();
            services.Configure<PluginOptions>(options =>
            {
                options.PluginDirectory = tempDir.FullName;
                options.ApiVersion = new Version(1, 0, 0);
            });

            using var provider = services.BuildServiceProvider();
            var loader = provider.GetRequiredService<PluginLoader>();

            var plugins = await loader.LoadAndRegisterAsync(tempDir.FullName, provider);

            Assert.Contains(plugins, plugin => plugin.Id == TestLifecyclePlugin.PluginId);
            Assert.True(TestLifecyclePlugin.OnLoadCalled);
            Assert.True(TestLifecyclePlugin.ConfigureServicesCalled);
            Assert.True(TestLifecyclePlugin.RegisterCalled);
            Assert.True(TestLifecyclePlugin.OnInitializeCalled);

            await loader.UnloadPluginAsync(TestLifecyclePlugin.PluginId);

            Assert.True(TestLifecyclePlugin.OnUnloadCalled);
            Assert.True(TestLifecyclePlugin.UnloadCalled);
        }
        finally
        {
            try
            {
                tempDir.Delete(true);
            }
            catch
            {
                // ignore cleanup failures
            }
        }
    }

    private sealed class TestLifecyclePlugin : INodePlugin
    {
        public const string PluginId = "com.nodeeditormax.test.lifecycle";
        public const string PluginName = "Lifecycle Test Plugin";

        public string Name => PluginName;

        public string Id => PluginId;

        public Version Version => new(1, 0, 0);

        public Version MinApiVersion => new(1, 0, 0);

        public static bool OnLoadCalled { get; private set; }

        public static bool ConfigureServicesCalled { get; private set; }

        public static bool RegisterCalled { get; private set; }

        public static bool OnInitializeCalled { get; private set; }

        public static bool OnUnloadCalled { get; private set; }

        public static bool UnloadCalled { get; private set; }

        public static void Reset()
        {
            OnLoadCalled = false;
            ConfigureServicesCalled = false;
            RegisterCalled = false;
            OnInitializeCalled = false;
            OnUnloadCalled = false;
            UnloadCalled = false;
        }

        public void Register(NodeRegistryService registry)
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

        private sealed class TestService;
    }
}