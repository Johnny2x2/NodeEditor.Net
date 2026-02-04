using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services;
using NodeEditor.Blazor.Services.Execution;
using NodeEditor.Blazor.Services.Plugins;
using NodeEditor.Blazor.Services.Registry;
using System.Diagnostics;
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
    private readonly PluginLoader _pluginLoader;
    private readonly NodeRegistryService _registry;
    private readonly NodeExecutionService _executionService;
    private readonly INodeContextRegistry _contextRegistry;
    private IReadOnlyList<INodePlugin>? _loadedPlugins;
    private readonly ITestOutputHelper output;

    public DynamicPluginLoadingTests(ITestOutputHelper output)
    {
        this.output = output;
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

    [Fact]
    public async Task DynamicLoad_LlmTornadoPlugin_CanCallOpenAIApi()
    {
        // Get API key from environment variable
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        
        // Skip if API key not available
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            output.WriteLine("SKIPPED: OPENAI_API_KEY environment variable not set");
            return;
        }

        // Find required node definitions
        var createApiDef = _registry.Definitions.FirstOrDefault(d => d.Name == "Create Tornado API");
        var createModelDef = _registry.Definitions.FirstOrDefault(d => d.Name == "Create Chat Model");
        var createAgentDef = _registry.Definitions.FirstOrDefault(d => d.Name == "Create Agent");
        var runAgentDef = _registry.Definitions.FirstOrDefault(d => d.Name == "Run Agent");
        var awaitMessageDef = _registry.Definitions.FirstOrDefault(d => d.Name == "Await Chat Message");

        // Skip if plugin not available
        if (createApiDef is null || createModelDef is null || createAgentDef is null || 
            runAgentDef is null || awaitMessageDef is null)
        {
            output.WriteLine("SKIPPED: LlmTornado plugin nodes not found");
            return;
        }

        // Create nodes
        var apiNode = createApiDef.Factory() with
        {
            Id = "api-1",
            Inputs = createApiDef.Factory().Inputs.Select(s => s.Name switch
            {
                "ApiKey" => s with { Value = SocketValue.FromObject(apiKey) },
                "BaseUrl" => s with { Value = SocketValue.FromObject("") },
                _ => s
            }).ToArray()
        };

        var modelNode = createModelDef.Factory() with
        {
            Id = "model-1",
            Inputs = createModelDef.Factory().Inputs.Select(s => s.Name switch
            {
                "ModelName" => s with { Value = SocketValue.FromObject("gpt-4o-mini") },
                _ => s
            }).ToArray()
        };

        var agentNode = createAgentDef.Factory() with
        {
            Id = "agent-1",
            Inputs = createAgentDef.Factory().Inputs.Select(s => s.Name switch
            {
                "Name" => s with { Value = SocketValue.FromObject("TestAgent") },
                "Instructions" => s with { Value = SocketValue.FromObject("You are a helpful assistant. Always respond with exactly: 'Test successful'") },
                "Streaming" => s with { Value = SocketValue.FromObject(false) },
                _ => s
            }).ToArray()
        };

        var runNode = runAgentDef.Factory() with
        {
            Id = "run-1",
            Inputs = runAgentDef.Factory().Inputs.Select(s => s.Name switch
            {
                "Input" => s with { Value = SocketValue.FromObject("Say test successful") },
                "AppendMessages" => s with { Value = SocketValue.FromObject(null) },
                "Streaming" => s with { Value = SocketValue.FromObject(false) },
                "SingleTurn" => s with { Value = SocketValue.FromObject(true) },
                _ => s
            }).ToArray()
        };

        var awaitNode = awaitMessageDef.Factory() with
        {
            Id = "await-1"
        };

        var nodes = new List<NodeData> { apiNode, modelNode, agentNode, runNode, awaitNode };

        // Create connections
        var connections = new List<ConnectionData>
        {
            new(apiNode.Id, modelNode.Id, "Client", "Client", false),
            new(apiNode.Id, agentNode.Id, "Client", "Client", false),
            new(modelNode.Id, agentNode.Id, "Model", "Model", false),
            new(agentNode.Id, runNode.Id, "Agent", "Agent", false),
            new(runNode.Id, awaitNode.Id, "ResultTask", "MessageTask", false),
            new(runNode.Id, awaitNode.Id, "Exit", "Enter", true) // Execution connection
        };

        // Execute with timeout
        var nodeContext = _contextRegistry.CreateCompositeContext();
        var executionContext = new NodeExecutionContext();
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await _executionService.ExecuteAsync(
            nodes,
            connections,
            executionContext,
            nodeContext,
            DefaultOptions,
            cts.Token);

        // Verify the agent ran successfully
        Assert.True(executionContext.IsNodeExecuted(runNode.Id), 
            "Run Agent node should have been executed");

        // Get the ChatMessage from await node
        var awaitMessage = executionContext.GetSocketValue(awaitNode.Id, "Message");
        Assert.NotNull(awaitMessage);
        
        // Extract text from ChatMessage using reflection
        var contentProp = awaitMessage.GetType().GetProperty("Content");
        Assert.NotNull(contentProp);
        
        var contentValue = contentProp.GetValue(awaitMessage);
        string? responseText = contentValue as string;
        
        // If Content is not a string, try to get Text property
        if (responseText == null && contentValue != null)
        {
            var textProp = contentValue.GetType().GetProperty("Text");
            if (textProp != null)
            {
                responseText = textProp.GetValue(contentValue) as string;
            }
        }
        
        Assert.NotNull(responseText);
        Assert.NotEmpty(responseText);
        output.WriteLine($"OpenAI API Response: {responseText}");
        
        // Verify it contains expected response
        Assert.Contains("successful", responseText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DynamicLoad_OpenCv2Plugin_CanProcessImage()
    {
        // Find required node definitions
        var loadImageDef = _registry.Definitions.FirstOrDefault(d => d.Name == "Load Image");
        var toGrayscaleDef = _registry.Definitions.FirstOrDefault(d => d.Name == "To Grayscale");
        var gaussianBlurDef = _registry.Definitions.FirstOrDefault(d => d.Name == "Gaussian Blur");
        var cannyDef = _registry.Definitions.FirstOrDefault(d => d.Name == "Canny");
        var getSizeDef = _registry.Definitions.FirstOrDefault(d => d.Name == "Get Size");

        // Skip if plugin not available
        if (loadImageDef is null || toGrayscaleDef is null || gaussianBlurDef is null || 
            cannyDef is null || getSizeDef is null)
        {
            output.WriteLine("SKIPPED: OpenCV2 plugin nodes not found");
            return;
        }

        // Create a simple test image in memory (100x100 white square)
        var testImagePath = Path.Combine(Path.GetTempPath(), $"test_image_{Guid.NewGuid()}.png");
        try
        {
            // Create a simple test image using OpenCvSharp directly (we can reference it in tests)
            using (var testImage = new OpenCvSharp.Mat(100, 100, OpenCvSharp.MatType.CV_8UC3, new OpenCvSharp.Scalar(255, 255, 255)))
            {
                // Draw a black rectangle in the middle
                OpenCvSharp.Cv2.Rectangle(testImage, new OpenCvSharp.Point(25, 25), new OpenCvSharp.Point(75, 75), new OpenCvSharp.Scalar(0, 0, 0), -1);
                OpenCvSharp.Cv2.ImWrite(testImagePath, testImage);
            }

            // Create nodes
            var loadNode = loadImageDef.Factory() with
            {
                Id = "load-1",
                Inputs = loadImageDef.Factory().Inputs.Select(s => s.Name switch
                {
                    "FilePath" => s with { Value = SocketValue.FromObject(testImagePath) },
                    "Mode" => s with { Value = SocketValue.FromObject(OpenCvSharp.ImreadModes.Color) },
                    _ => s
                }).ToArray()
            };

            var grayNode = toGrayscaleDef.Factory() with
            {
                Id = "gray-1"
            };

            var blurNode = gaussianBlurDef.Factory() with
            {
                Id = "blur-1",
                Inputs = gaussianBlurDef.Factory().Inputs.Select(s => s.Name switch
                {
                    "KernelSize" => s with { Value = SocketValue.FromObject(5) },
                    "Sigma" => s with { Value = SocketValue.FromObject(1.5) },
                    _ => s
                }).ToArray()
            };

            var cannyNode = cannyDef.Factory() with
            {
                Id = "canny-1",
                Inputs = cannyDef.Factory().Inputs.Select(s => s.Name switch
                {
                    "Threshold1" => s with { Value = SocketValue.FromObject(50.0) },
                    "Threshold2" => s with { Value = SocketValue.FromObject(150.0) },
                    _ => s
                }).ToArray()
            };

            var sizeNode = getSizeDef.Factory() with
            {
                Id = "size-1"
            };

            var nodes = new List<NodeData> { loadNode, grayNode, blurNode, cannyNode, sizeNode };

            // Create connections: Load → Gray → Blur → Canny → Size
            var connections = new List<ConnectionData>
            {
                new(loadNode.Id, grayNode.Id, "Image", "Image", false),
                new(grayNode.Id, blurNode.Id, "Result", "Image", false),
                new(blurNode.Id, cannyNode.Id, "Result", "Image", false),
                new(cannyNode.Id, sizeNode.Id, "Result", "Image", false)
            };

            // Execute
            var nodeContext = _contextRegistry.CreateCompositeContext();
            var executionContext = new NodeExecutionContext();

            // Resolve data-flow nodes (they're all isCallable: false) - MUST execute in dependency order
            await ResolveDataFlowNode(loadNode, connections, executionContext, nodeContext);
            await ResolveDataFlowNode(grayNode, connections, executionContext, nodeContext);
            await ResolveDataFlowNode(blurNode, connections, executionContext, nodeContext);
            await ResolveDataFlowNode(cannyNode, connections, executionContext, nodeContext);
            await ResolveDataFlowNode(sizeNode, connections, executionContext, nodeContext);

            // Debug: Print actual output sockets for the size node
            output.WriteLine($"Size node outputs: {string.Join(", ", sizeNode.Outputs.Select(o => o.Name))}");
            output.WriteLine($"Size node definition ID: {sizeNode.DefinitionId}");

            // Verify the final size output
            var width = executionContext.GetSocketValue(sizeNode.Id, "Width");
            var height = executionContext.GetSocketValue(sizeNode.Id, "Height");

            output.WriteLine($"Width value: {width}, Height value: {height}");

            Assert.NotNull(width);
            Assert.NotNull(height);
            Assert.Equal(100, width);
            Assert.Equal(100, height);

            output.WriteLine($"Successfully processed image: {width}x{height}");
        }
        finally
        {
            // Cleanup
            if (File.Exists(testImagePath))
            {
                File.Delete(testImagePath);
            }
        }
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
        
        if (nodeContext is NodeEditor.Blazor.Services.Execution.CompositeNodeContext composite)
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
