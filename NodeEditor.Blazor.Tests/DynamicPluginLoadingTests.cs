using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services;
using NodeEditor.Blazor.Services.Execution;
using NodeEditor.Blazor.Services.Plugins;
using NodeEditor.Blazor.Services.Registry;

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
    private readonly PluginLoader _pluginLoader;
    private readonly NodeRegistryService _registry;
    private readonly NodeExecutionService _executionService;
    private readonly INodeContextRegistry _contextRegistry;
    private IReadOnlyList<INodePlugin>? _loadedPlugins;

    public DynamicPluginLoadingTests()
    {
        // Use the plugins folder that MSBuild copies to during test build
        _testPluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<NodeDiscoveryService>();
        services.AddSingleton<NodeRegistryService>();
        services.AddSingleton<IPluginServiceRegistry, PluginServiceRegistry>();
        services.AddSingleton<INodeContextRegistry, NodeContextRegistry>();
        services.AddSingleton<PluginLoader>();
        services.AddSingleton<ExecutionPlanner>();
        services.AddSingleton<NodeExecutionService>();
        services.AddSingleton<SocketTypeResolver>();
        services.Configure<PluginOptions>(options =>
        {
            options.PluginDirectory = _testPluginsDir;
            options.ApiVersion = new Version(1, 0, 0);
            options.EnablePluginLoading = true;
        });

        _serviceProvider = services.BuildServiceProvider();
        _pluginLoader = _serviceProvider.GetRequiredService<PluginLoader>();
        _registry = _serviceProvider.GetRequiredService<NodeRegistryService>();
        _executionService = _serviceProvider.GetRequiredService<NodeExecutionService>();
        _contextRegistry = _serviceProvider.GetRequiredService<INodeContextRegistry>();
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

    private static NodeExecutionOptions DefaultOptions => new(
        ExecutionMode.Sequential, 
        AllowBackground: false, 
        MaxDegreeOfParallelism: 1);

    [Fact]
    public void DynamicLoad_SamplePlugin_LoadsWithoutCompileTimeReference()
    {
        // Assert - Sample plugin should be loaded
        Assert.NotNull(_loadedPlugins);
        Assert.Contains(_loadedPlugins, p => p.Id == "com.nodeeditormax.sample");

        // Verify nodes are registered
        var catalog = _registry.GetCatalog();
        Assert.Contains(catalog.All, n => n.Name == "Multiply");
        Assert.Contains(catalog.All, n => n.Name == "Clamp");
        Assert.Contains(catalog.All, n => n.Name == "Random Int");
        Assert.Contains(catalog.All, n => n.Name == "Pulse");
    }

    [Fact]
    public async Task DynamicLoad_SamplePlugin_CanExecuteMultiplyNode()
    {
        // Find the Multiply node definition
        var multiplyDef = _registry.Definitions.FirstOrDefault(d => d.Name == "Multiply");
        Assert.NotNull(multiplyDef);

        // Create node instance using the Factory
        var multiplyNode = multiplyDef.Factory();
        multiplyNode = multiplyNode with
        {
            Inputs = multiplyNode.Inputs.Select(s => s.Name switch
            {
                "A" => s with { Value = SocketValue.FromObject(6.0) },
                "B" => s with { Value = SocketValue.FromObject(7.0) },
                _ => s
            }).ToArray()
        };

        var nodes = new List<NodeData> { multiplyNode };
        var connections = new List<ConnectionData>();

        // Create context from currently loaded assemblies - must be done AFTER plugins are loaded
        // The InitializeAsync already loaded plugins, so assemblies should be in AppDomain
        var nodeContext = _contextRegistry.CreateCompositeContext();

        // Verify the context has the SamplePluginContext
        var sampleContext = nodeContext.Contexts.FirstOrDefault(c => c.GetType().Name == "SamplePluginContext");
        Assert.NotNull(sampleContext);

        // Verify the context has a Multiply method
        var multiplyMethod = sampleContext.GetType().GetMethods()
            .FirstOrDefault(m => m.Name == "Multiply" || 
                m.GetCustomAttributes().Any(a => a.GetType().Name == "NodeAttribute"));
        Assert.NotNull(multiplyMethod);

        var executionContext = new NodeExecutionContext();

        await _executionService.ExecuteAsync(
            nodes, 
            connections, 
            executionContext, 
            nodeContext, 
            DefaultOptions,
            CancellationToken.None);

        // Check if the node was executed
        Assert.True(executionContext.IsNodeExecuted(multiplyNode.Id), 
            "Multiply node should have been executed");

        // Assert - Result should be 42
        var result = executionContext.GetSocketValue(multiplyNode.Id, "Result");
        Assert.Equal(42.0, result);
    }

    [Fact]
    public void DynamicLoad_LlmTornadoPlugin_LoadsWithDependencies()
    {
        // This test verifies that LlmTornado plugin loads WITH all its dependencies
        // (LlmTornado.dll, LlmTornado.Agents.dll, etc.) even though we don't have
        // compile-time references to them.

        // Assert - LlmTornado plugin should be loaded
        var llmPlugin = _loadedPlugins?.FirstOrDefault(p => p.Id == "com.nodeeditormax.llmtornado");

        // Skip if plugin not available (may not be built)
        if (llmPlugin is null)
        {
            return; // Plugin not in test plugins folder
        }

        // Verify LLM Tornado nodes are registered
        var catalog = _registry.GetCatalog();
        Assert.Contains(catalog.All, n => n.Name == "Create Tornado API");
        Assert.Contains(catalog.All, n => n.Name == "Create Agent");
        Assert.Contains(catalog.All, n => n.Name == "Run Agent");
        Assert.Contains(catalog.All, n => n.Name == "Create User Message");
    }

    [Fact]
    public async Task DynamicLoad_LlmTornadoPlugin_CanExecuteCreateUserMessageNode()
    {
        // Find the Create User Message node
        var nodeDef = _registry.Definitions.FirstOrDefault(d => d.Name == "Create User Message");

        // Skip if plugin not available
        if (nodeDef is null)
        {
            return;
        }

        // Create node instance with input
        var node = nodeDef.Factory();
        node = node with
        {
            Inputs = node.Inputs.Select(s => s.Name switch
            {
                "Text" => s with { Value = SocketValue.FromObject("Hello from dynamic test!") },
                _ => s
            }).ToArray()
        };

        var nodes = new List<NodeData> { node };
        var connections = new List<ConnectionData>();

        // Execute using dynamically loaded context
        var nodeContext = _contextRegistry.CreateCompositeContext();
        var executionContext = new NodeExecutionContext();

        await _executionService.ExecuteAsync(
            nodes,
            connections,
            executionContext,
            nodeContext,
            DefaultOptions,
            CancellationToken.None);

        // Assert - Output should be a ChatMessage
        var result = executionContext.GetSocketValue(node.Id, "Message");
        Assert.NotNull(result);

        // Verify it's actually a ChatMessage type (loaded dynamically)
        var resultType = result.GetType();
        Assert.Equal("ChatMessage", resultType.Name);
    }

    [Fact]
    public async Task DynamicLoad_PluginDependencies_ResolveCorrectly()
    {
        // This test specifically verifies that plugin dependencies are loaded
        // by attempting to use a type from a dependency (not the plugin itself)

        // Find a node that uses LlmTornado types
        var nodeDef = _registry.Definitions.FirstOrDefault(d => d.Name == "Create Chat Model");

        if (nodeDef is null)
        {
            return; // Plugin not available
        }

        // Create and execute the node
        var node = nodeDef.Factory();
        node = node with
        {
            Inputs = node.Inputs.Select(s => s.Name switch
            {
                "ModelName" => s with { Value = SocketValue.FromObject("gpt-4") },
                _ => s
            }).ToArray()
        };

        var nodeContext = _contextRegistry.CreateCompositeContext();
        var executionContext = new NodeExecutionContext();

        await _executionService.ExecuteAsync(
            new List<NodeData> { node },
            new List<ConnectionData>(),
            executionContext,
            nodeContext,
            DefaultOptions,
            CancellationToken.None);

        // The fact that this executes without FileNotFoundException or 
        // TypeLoadException proves dependencies are loaded correctly
        var result = executionContext.GetSocketValue(node.Id, "Model");
        Assert.NotNull(result);
        Assert.Equal("ChatModel", result.GetType().Name);
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
            return; // Sample plugin not available
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

        // Execute
        var nodeContext = _contextRegistry.CreateCompositeContext();
        var executionContext = new NodeExecutionContext();

        await _executionService.ExecuteAsync(
            nodes,
            connections,
            executionContext,
            nodeContext,
            DefaultOptions,
            CancellationToken.None);

        // Multiply: 10 * 5 = 50, Clamp(50, 0, 40) = 40
        var result = executionContext.GetSocketValue(clampNode.Id, "Result");
        Assert.Equal(40.0, result);
    }
}
