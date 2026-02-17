using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Net.Models;
using NodeEditor.Net.Services;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Net.Services.Plugins;
using NodeEditor.Net.Services.Registry;
using System.IO;
using System.Reflection;
using Xunit.Abstractions;

namespace NodeEditor.Blazor.Tests;

/// <summary>
/// Tests that verify plugins can be loaded dynamically without compile-time references,
/// including proper loading of all dependencies.
/// 
/// These tests intentionally do NOT reference plugin assemblies directly.
/// They rely on the PluginLoader to discover and load plugins from the plugins folder.
/// </summary>
public sealed class DynamicPluginLoadingTests : IAsyncLifetime
{
    private readonly string _testPluginsDir;
    private readonly ServiceProvider _serviceProvider;
    private readonly IPluginLoader _pluginLoader;
    private readonly INodeRegistryService _registry;
    private readonly INodeExecutionService _executionService;
    private IReadOnlyList<INodePlugin>? _loadedPlugins;
    private readonly ITestOutputHelper output;

    public DynamicPluginLoadingTests(ITestOutputHelper output)
    {
        this.output = output;
        _testPluginsDir = PrepareTestPluginsDir();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<NodeDiscoveryService>();
        services.AddSingleton<INodeRegistryService, NodeRegistryService>();
        services.AddSingleton<IPluginServiceRegistry, PluginServiceRegistry>();
        services.AddSingleton<IPluginLoader, PluginLoader>();
        services.AddSingleton<ExecutionPlanner>();
        services.AddSingleton<INodeExecutionService, NodeExecutionService>();
        services.AddSingleton<ISocketTypeResolver, SocketTypeResolver>();
        services.Configure<PluginOptions>(options =>
        {
            options.PluginDirectory = _testPluginsDir;
            options.ApiVersion = new Version(1, 0, 0);
            options.EnablePluginLoading = true;
        });

        _serviceProvider = services.BuildServiceProvider();
        _pluginLoader = _serviceProvider.GetRequiredService<IPluginLoader>();
        _registry = _serviceProvider.GetRequiredService<INodeRegistryService>();
        _executionService = _serviceProvider.GetRequiredService<INodeExecutionService>();
    }

    public async Task InitializeAsync()
    {
        _loadedPlugins = await _pluginLoader.LoadAndRegisterAsync(_testPluginsDir);
    }

    public Task DisposeAsync()
    {
        _serviceProvider.Dispose();
        return Task.CompletedTask;
    }

    private static string PrepareTestPluginsDir()
    {
        var repoRoot = FindRepoRoot();
        if (repoRoot is null)
        {
            return Path.Combine(AppContext.BaseDirectory, "plugins");
        }

        var projectRoot = Path.Combine(repoRoot, "NodeEditor.Plugins.TestA");
        var debugOutput = Path.Combine(projectRoot, "bin", "Debug", "net10.0");
        var releaseOutput = Path.Combine(projectRoot, "bin", "Release", "net10.0");
        string sourceDir;
        if (Directory.Exists(debugOutput))
            sourceDir = debugOutput;
        else if (Directory.Exists(releaseOutput))
            sourceDir = releaseOutput;
        else
            sourceDir = Path.Combine(repoRoot, "plugin-repository", "NodeEditor.Plugins.TestA");

        if (!Directory.Exists(sourceDir))
        {
            return Path.Combine(AppContext.BaseDirectory, "plugins");
        }

        var targetRoot = Path.Combine(AppContext.BaseDirectory, "plugins-test", Guid.NewGuid().ToString("N"));
        var targetDir = Path.Combine(targetRoot, "TestA");
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            if (fileName.Equals("NodeEditor.Net.dll", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("NodeEditor.Blazor.dll", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var dest = Path.Combine(targetDir, fileName);
            File.Copy(file, dest, overwrite: false);
        }

        var manifestPath = Path.Combine(projectRoot, "plugin.json");
        if (File.Exists(manifestPath))
        {
            File.Copy(manifestPath, Path.Combine(targetDir, "plugin.json"), overwrite: true);
        }

        return targetRoot;
    }

    private static string? FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var slnx = Path.Combine(current.FullName, "NodeEditor.slnx");
            if (File.Exists(slnx))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private static NodeExecutionOptions DefaultOptions => new(
        ExecutionMode.Sequential, 
        AllowBackground: false, 
        MaxDegreeOfParallelism: 1);

    [Fact]
    public void DynamicLoad_PluginsLoad_WithoutCompileTimeReference()
    {
        // Assert - Plugins should be loaded
        Assert.NotNull(_loadedPlugins);
        // Verify at least one plugin is loaded
        Assert.NotEmpty(_loadedPlugins);

        // Verify TestA nodes are registered
        var catalog = _registry.GetCatalog();
        Assert.Contains(catalog.All, n => n.Name == "Echo String");
        Assert.Contains(catalog.All, n => n.Name == "Ping");
        Assert.Contains(catalog.All, n => n.Name == "Load Image");
    }

    [Fact]
    public async Task DynamicLoad_TestPlugins_CanExecuteNodes()
    {
        // Use PingNode which is an ExecutionInitiator – the engine will trigger it.
        var pingDef = _registry.Definitions.FirstOrDefault(d => d.Name == "Ping");
        Assert.NotNull(pingDef);

        // Verify it was discovered as a NodeBase subclass
        Assert.NotNull(pingDef.NodeType);

        // Create node instance using the Factory
        var pingNode = pingDef.Factory();

        var nodes = new List<NodeData> { pingNode };
        var connections = new List<ConnectionData>();

        var context = new NodeRuntimeStorage();

        await _executionService.ExecuteAsync(
            nodes, 
            connections, 
            context, 
            null!, 
            DefaultOptions,
            CancellationToken.None);

        // Check if the node was executed (PingNode is an initiator so the engine runs it)
        Assert.True(context.IsNodeExecuted(pingNode.Id), 
            "Ping node should have been executed");
    }

    [Fact]
    public async Task DynamicLoad_PluginScopedServices_AreResolvedDuringExecution()
    {
        var probeDef = _registry.Definitions.FirstOrDefault(d => d.Name == "Plugin Service Probe");
        Assert.NotNull(probeDef);

        var probeNode = probeDef!.Factory();

        var nodes = new List<NodeData> { probeNode };
        var connections = new List<ConnectionData>();
        var context = new NodeRuntimeStorage();

        await _executionService.ExecuteAsync(
            nodes,
            connections,
            context,
            null!,
            DefaultOptions,
            CancellationToken.None);

        Assert.True(context.IsNodeExecuted(probeNode.Id),
            "Plugin service probe node should have been executed");
        Assert.True(context.GetSocketValue(probeNode.Id, "Ok") as bool? == true,
            "Probe node should resolve plugin-scoped service successfully");
        Assert.Equal("plugin-service-ok", context.GetSocketValue(probeNode.Id, "Value") as string);
    }


    [Fact]
    public void DynamicLoad_AllPlugins_NoAssemblyLoadExceptions()
    {
        // This test loads all plugins and verifies no assembly load exceptions occur

        Exception? caughtException = null;

        try
        {
            // Try to access all node definitions (forces type loading)
            foreach (var definition in _registry.Definitions)
            {
                // Create an instance of each node to force full type resolution
                _ = definition.Factory();
            }
        }
        catch (Exception ex) when (
            ex is FileNotFoundException || 
            ex is TypeLoadException || 
            ex is System.Reflection.ReflectionTypeLoadException)
        {
            caughtException = ex;
        }

        Assert.Null(caughtException);
    }

    [Fact]
    public async Task DynamicLoad_ExecuteFullGraph_WithDynamicallyLoadedNodes()
    {
        // Build a small graph using dynamically loaded nodes
        var multiplyDef = _registry.Definitions.FirstOrDefault(d => d.Name == "Multiply");
        var clampDef = _registry.Definitions.FirstOrDefault(d => d.Name == "Clamp");

        if (multiplyDef is null || clampDef is null)
        {
            return; // Required plugin nodes not available
        }

        // Create nodes: Multiply(10, 5) -> Clamp(0, 40)
        var multiplyNode = multiplyDef.Factory() with
        {
            Id = "multiply-1",
            Inputs = multiplyDef.Factory().Inputs.Select(s => s.Name switch
            {
                "A" => s with { Value = SocketValue.FromObject(10.0) },
                "B" => s with { Value = SocketValue.FromObject(5.0) },
                _ => s
            }).ToArray()
        };

        var clampNode = clampDef.Factory() with
        {
            Id = "clamp-1",
            Inputs = clampDef.Factory().Inputs.Select(s => s.Name switch
            {
                "Min" => s with { Value = SocketValue.FromObject(0.0) },
                "Max" => s with { Value = SocketValue.FromObject(40.0) },
                _ => s
            }).ToArray()
        };

        var nodes = new List<NodeData> { multiplyNode, clampNode };

        // Connect Multiply.Result -> Clamp.Value
        var connections = new List<ConnectionData>
        {
            new ConnectionData(
                OutputNodeId: multiplyNode.Id, 
                InputNodeId: clampNode.Id,
                OutputSocketName: "Result",
                InputSocketName: "Value",
                IsExecution: false)
        };

        // Execute using the new NodeBase-based execution engine
        var context = new NodeRuntimeStorage();

        await _executionService.ExecuteAsync(
            nodes,
            connections,
            context,
            null!,
            DefaultOptions,
            CancellationToken.None);

        // Multiply: 10 * 5 = 50, Clamp(50, 0, 40) = 40
        var result = context.GetSocketValue(clampNode.Id, "Result");
        Assert.Equal(40.0, result);
    }
}
