using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Net.Models;
using NodeEditor.Net.Services;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Net.Services.Plugins;
using NodeEditor.Net.Services.Registry;
using System.Diagnostics;
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
    private readonly INodeContextRegistry _contextRegistry;
    private IReadOnlyList<INodePlugin>? _loadedPlugins;
    private readonly ITestOutputHelper output;

    public DynamicPluginLoadingTests(ITestOutputHelper output)
    {
        this.output = output;
        // Prefer the packaged TestA plugin from the repo for dynamic loading tests
        _testPluginsDir = PrepareTestPluginsDir();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<NodeDiscoveryService>();
        services.AddSingleton<INodeRegistryService, NodeRegistryService>();
        services.AddSingleton<IPluginServiceRegistry, PluginServiceRegistry>();
        services.AddSingleton<INodeContextRegistry, NodeContextRegistry>();
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

    private static string PrepareTestPluginsDir()
    {
        var repoRoot = FindRepoRoot();
        if (repoRoot is null)
        {
            return Path.Combine(AppContext.BaseDirectory, "plugins");
        }

        var projectRoot = Path.Combine(repoRoot, "NodeEditor.Plugins.TestA");
        var buildOutput = Path.Combine(projectRoot, "bin", "Debug", "net10.0");
        var sourceDir = Directory.Exists(buildOutput)
            ? buildOutput
            : Path.Combine(repoRoot, "plugin-repository", "NodeEditor.Plugins.TestA");

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
        // Find the Echo String node definition
        var echoDef = _registry.Definitions.FirstOrDefault(d => d.Name == "Echo String");
        Assert.NotNull(echoDef);

        // Create node instance using the Factory
        var echoNode = echoDef.Factory();
        echoNode = echoNode with
        {
            Inputs = echoNode.Inputs.Select(s => s.Name switch
            {
                "Input" => s with { Value = SocketValue.FromObject("Hello") },
                _ => s
            }).ToArray()
        };

        var nodes = new List<NodeData> { echoNode };
        var connections = new List<ConnectionData>();

        // Create context from currently loaded assemblies - must be done AFTER plugins are loaded
        // The InitializeAsync already loaded plugins, so assemblies should be in AppDomain
        var nodeContext = _contextRegistry.CreateCompositeContext();

        // Verify the context has loaded contexts
        Assert.NotEmpty(nodeContext.Contexts);



        var executionContext = new NodeExecutionContext();

        await _executionService.ExecuteAsync(
            nodes, 
            connections, 
            executionContext, 
            nodeContext, 
            DefaultOptions,
            CancellationToken.None);

        // Check if the node was executed
        Assert.True(executionContext.IsNodeExecuted(echoNode.Id), 
            "Echo node should have been executed");

        // Assert - Output should match input
        var result = executionContext.GetSocketValue(echoNode.Id, "Output");
        Assert.Equal("Hello", result);
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



    private async Task ResolveDataFlowNode(
        NodeData node,
        List<ConnectionData> connections,
        NodeExecutionContext executionContext,
        INodeContext nodeContext)
    {
        output.WriteLine($"ResolveDataFlowNode called for node {node.Id}, DefinitionId: {node.DefinitionId}");
        
        // Find the method definition - parse from DefinitionId
        // Format: "Namespace.ClassName.MethodName(ParamType1,ParamType2,...)"
        // First split by '(' to get everything before parameters
        // Then split that by '.' and take the last part to get just the method name
        var methodName = node.DefinitionId != null
            ? node.DefinitionId.Split('(')[0].Split('.')[^1]
            : null;
        output.WriteLine($"Extracted method name: '{methodName}'");

        // Handle CompositeNodeContext - need to find the actual context that has the method
        object? actualContext = nodeContext;
        System.Reflection.MethodInfo? methodInfo = null;
        
        if (nodeContext is NodeEditor.Net.Services.Execution.CompositeNodeContext composite)
        {
            output.WriteLine($"NodeContext is CompositeNodeContext with {composite.Contexts.Count} contexts");
            
            // Search through all contexts for the method
            foreach (var ctx in composite.Contexts)
            {
                var method = ctx.GetType().GetMethod(methodName ?? string.Empty,
                    System.Reflection.BindingFlags.Public | 
                    System.Reflection.BindingFlags.Instance | 
                    System.Reflection.BindingFlags.IgnoreCase);
                
                if (method != null)
                {
                    output.WriteLine($"Found method '{methodName}' in context type: {ctx.GetType().FullName}");
                    methodInfo = method;
                    actualContext = ctx;
                    break;
                }
            }
        }
        else
        {
            // Single context - search directly
            methodInfo = methodName != null
                ? nodeContext.GetType().GetMethod(methodName, 
                    System.Reflection.BindingFlags.Public | 
                    System.Reflection.BindingFlags.Instance | 
                    System.Reflection.BindingFlags.IgnoreCase)
                : null;
        }

        output.WriteLine($"MethodInfo found: {(methodInfo != null ? methodInfo.Name : "null")}");
        
        if (methodInfo == null || actualContext == null) return;

        // Build input value dictionary
        var inputValues = new Dictionary<string, object?>();

        // Resolve inputs from connections
        foreach (var conn in connections.Where(c => c.InputNodeId == node.Id))
        {
            var sourceSocketName = conn.InputSocketName;
            var sourceValue = executionContext.GetSocketValue(conn.OutputNodeId, conn.OutputSocketName);
            
            if (sourceValue != null)
            {
                inputValues[sourceSocketName] = sourceValue;
            }
        }

        // Add values from node sockets if they have values and no connection
        foreach (var input in node.Inputs.Where(i => !connections.Any(c => c.InputNodeId == node.Id && c.InputSocketName == i.Name)))
        {
            if (input.Value.Json.HasValue)
            {
                inputValues[input.Name] = input.Value.ToObject<object>();
            }
        }

        // Build parameter array for method invocation
        var parameters = methodInfo.GetParameters();
        var args = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            if (!param.IsOut && inputValues.TryGetValue(param.Name!, out var value))
            {
                // Handle type conversion - if value is JsonElement, deserialize to proper type
                if (value is System.Text.Json.JsonElement jsonElement)
                {
                    args[i] = System.Text.Json.JsonSerializer.Deserialize(jsonElement.GetRawText(), param.ParameterType);
                }
                else
                {
                    args[i] = value;
                }
            }
        }

        // Invoke the method using the actual context (not composite wrapper)
        methodInfo.Invoke(actualContext, args);

        output.WriteLine($"After method invocation, args: {string.Join(", ", args.Select((a, i) => $"[{i}]={a ?? "null"}"))}");

        // Extract out parameters and store them as outputs
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].IsOut)
            {
                var outputName = parameters[i].Name!.TrimStart('_'); // Remove leading underscore if any
                var outputSocket = node.Outputs.FirstOrDefault(o => 
                    string.Equals(o.Name, outputName, StringComparison.OrdinalIgnoreCase));
                
                output.WriteLine($"Out param [{i}]: name='{parameters[i].Name}', value={args[i] ?? "null"}, matched socket={outputSocket?.Name ?? "none"}");
                
                if (outputSocket != null && args[i] != null)
                {
                    executionContext.SetSocketValue(node.Id, outputSocket.Name, args[i]);
                }
            }
        }
    }
}
