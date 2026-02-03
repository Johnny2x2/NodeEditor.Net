using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using LlmTornado.Chat;
using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services.Execution;
using NodeEditor.Blazor.Services;
using NodeEditor.Blazor.Services.Plugins;
using NodeEditor.Blazor.Services.Registry;
using Xunit;

namespace NodeEditor.Blazor.Tests;

/// <summary>
/// Tests for the LlmTornado plugin that load it DYNAMICALLY, not via compile-time reference.
/// This validates the plugin loading mechanism works correctly with all dependencies.
/// </summary>
public sealed class LlmTornadoPluginTests : IAsyncLifetime
{
    private readonly ServiceProvider _serviceProvider;
    private readonly PluginLoader _pluginLoader;
    private readonly NodeRegistryService _registry;
    private readonly NodeExecutionService _executionService;
    private readonly INodeContextRegistry _contextRegistry;
    private bool _pluginLoaded;

    public LlmTornadoPluginTests()
    {
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
            options.PluginDirectory = Path.Combine(AppContext.BaseDirectory, "plugins");
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
        // Load plugins dynamically
        var plugins = await _pluginLoader.LoadAndRegisterAsync();
        _pluginLoaded = plugins.Any(p => p.Id == "com.nodeeditormax.llmtornado");
    }

    public Task DisposeAsync()
    {
        _serviceProvider.Dispose();
        return Task.CompletedTask;
    }

    private void SkipIfPluginNotLoaded()
    {
        if (!_pluginLoaded)
        {
            Assert.Fail("LlmTornado plugin not loaded - ensure it's built and copied to plugins folder");
        }
    }

    [Fact]
    public void PluginRegistersCoreNodes()
    {
        SkipIfPluginNotLoaded();

        var definitions = _registry.Definitions;
        Assert.Contains(definitions, definition => definition.Name == "Create Agent");
        Assert.Contains(definitions, definition => definition.Name == "Run Agent");
        Assert.Contains(definitions, definition => definition.Name == "MCP Initialize");
        Assert.Contains(definitions, definition => definition.Name == "Create Image Part");
        Assert.Contains(definitions, definition => definition.Name == "Invoke Orchestration");
    }

    [Fact]
    public void AsyncNodesExposeTaskOutputs()
    {
        SkipIfPluginNotLoaded();

        // Get the node context type dynamically from loaded assemblies
        var contextType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return []; } })
            .FirstOrDefault(t => t.Name == "LlmTornadoNodeContext");

        Assert.NotNull(contextType);

        var methods = contextType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        var method = methods.Single(m => m.Name == "RunAgentAsync");
        Assert.Contains(method.GetParameters(), p => p.IsOut && IsTaskOf(p.ParameterType, "LlmTornado.Chat.ChatMessage"));

        method = methods.Single(m => m.Name == "InitializeMcpServerAsync");
        Assert.Contains(method.GetParameters(), p => p.IsOut && IsTaskOfList(p.ParameterType, "LlmTornado.Common.Tool"));

        method = methods.Single(m => m.Name == "InvokeOrchestrationAsync");
        Assert.Contains(method.GetParameters(), p => p.IsOut && IsTaskOf(p.ParameterType, "LlmTornado.Chat.ChatMessage"));

        method = methods.Single(m => m.Name == "InvokeChatRuntimeAsync");
        Assert.Contains(method.GetParameters(), p => p.IsOut && IsTaskOf(p.ParameterType, "LlmTornado.Chat.ChatMessage"));
    }

    [Fact]
    public async Task MessageNodes_TextRoundTrip_WiresCorrectly()
    {
        SkipIfPluginNotLoaded();

        var nodes = new List<NodeData>
        {
            CreateUserMessageNode("msg", "Hello from nodes"),
            MessageToTextNode("extract")
        };

        var connections = new List<ConnectionData>
        {
            DataConnection("msg", "extract", "Message", "Message")
        };

        var context = new NodeExecutionContext();
        var nodeContext = _contextRegistry.CreateCompositeContext();

        await _executionService.ExecuteAsync(nodes, connections, context, nodeContext, PlannedOptions, CancellationToken.None);

        Assert.Equal("Hello from nodes", context.GetSocketValue("extract", "Text"));
    }

    [Fact]
    public async Task PartNodes_CreateParts_PopulatesOutputs()
    {
        SkipIfPluginNotLoaded();

        var nodes = new List<NodeData>
        {
            CreateTextPartNode("textPart", "Hello"),
            CreateImagePartNode("imagePart", "https://example.com/image.png")
        };

        var context = new NodeExecutionContext();
        var nodeContext = _contextRegistry.CreateCompositeContext();

        await _executionService.ExecuteAsync(nodes, Array.Empty<ConnectionData>(), context, nodeContext, PlannedOptions, CancellationToken.None);

        var textPart = context.GetSocketValue("textPart", "Part");
        var imagePart = context.GetSocketValue("imagePart", "Part");

        Assert.IsType<ChatMessagePart>(textPart);
        Assert.IsType<ChatMessagePart>(imagePart);
    }

    [Fact]
    public async Task UserMessageFromParts_NodeBuildsMessage()
    {
        SkipIfPluginNotLoaded();

        var parts = new List<ChatMessagePart>
        {
            new("Hello"),
            new(new Uri("https://example.com/image.png"))
        };

        var nodes = new List<NodeData>
        {
            CreateUserMessageFromPartsNode("parts")
        };

        var context = new NodeExecutionContext();
        context.SetSocketValue("parts", "Parts", parts);

        var nodeContext = _contextRegistry.CreateCompositeContext();

        await _executionService.ExecuteAsync(nodes, Array.Empty<ConnectionData>(), context, nodeContext, PlannedOptions, CancellationToken.None);

        var message = context.GetSocketValue("parts", "Message");
        Assert.IsType<ChatMessage>(message);
    }

    private static bool IsTaskOf(Type type, string fullName)
    {
         var targetType = type.IsByRef ? type.GetElementType() ?? type : type;

         return targetType.IsGenericType
             && targetType.GetGenericTypeDefinition() == typeof(Task<>)
             && string.Equals(targetType.GetGenericArguments()[0].FullName, fullName, StringComparison.Ordinal);
    }

    private static bool IsTaskOfList(Type type, string elementFullName)
    {
        var targetType = type.IsByRef ? type.GetElementType() ?? type : type;

        if (!targetType.IsGenericType || targetType.GetGenericTypeDefinition() != typeof(Task<>))
        {
            return false;
        }

        var innerType = targetType.GetGenericArguments()[0];
        return innerType.IsGenericType
               && innerType.GetGenericTypeDefinition() == typeof(List<>)
               && string.Equals(innerType.GetGenericArguments()[0].FullName, elementFullName, StringComparison.Ordinal);
    }

    private static NodeExecutionOptions PlannedOptions
        => new(ExecutionMode.Parallel, AllowBackground: false, MaxDegreeOfParallelism: 1);

    private static NodeData CreateUserMessageNode(string id, string text)
        => new(id, "Create User Message", false, false,
            Inputs: new[]
            {
                DataInput("Text", typeof(string).FullName ?? "System.String", SocketValue.FromObject(text))
            },
            Outputs: new[]
            {
                DataOutput("Message", typeof(ChatMessage).FullName ?? "LlmTornado.Chat.ChatMessage")
            });

    private static NodeData CreateUserMessageFromPartsNode(string id)
        => new(id, "Create User Message (Parts)", false, false,
            Inputs: new[]
            {
                DataInput("Parts", typeof(List<ChatMessagePart>).FullName ?? "System.Collections.Generic.List`1")
            },
            Outputs: new[]
            {
                DataOutput("Message", typeof(ChatMessage).FullName ?? "LlmTornado.Chat.ChatMessage")
            });

    private static NodeData MessageToTextNode(string id)
        => new(id, "Message To Text", false, false,
            Inputs: new[]
            {
                DataInput("Message", typeof(ChatMessage).FullName ?? "LlmTornado.Chat.ChatMessage")
            },
            Outputs: new[]
            {
                DataOutput("Text", typeof(string).FullName ?? "System.String")
            });

    private static NodeData CreateTextPartNode(string id, string text)
        => new(id, "Create Text Part", false, false,
            Inputs: new[]
            {
                DataInput("Text", typeof(string).FullName ?? "System.String", SocketValue.FromObject(text))
            },
            Outputs: new[]
            {
                DataOutput("Part", typeof(ChatMessagePart).FullName ?? "LlmTornado.Chat.ChatMessagePart")
            });

    private static NodeData CreateImagePartNode(string id, string url)
        => new(id, "Create Image Part", false, false,
            Inputs: new[]
            {
                DataInput("ImageUrl", typeof(string).FullName ?? "System.String", SocketValue.FromObject(url))
            },
            Outputs: new[]
            {
                DataOutput("Part", typeof(ChatMessagePart).FullName ?? "LlmTornado.Chat.ChatMessagePart")
            });

    private static ConnectionData DataConnection(string outputNodeId, string inputNodeId, string outputSocket, string inputSocket)
        => new(outputNodeId, inputNodeId, outputSocket, inputSocket, false);

    private static SocketData DataInput(string name, string typeName, SocketValue? value = null)
        => new(name, typeName, true, false, value);

    private static SocketData DataOutput(string name, string typeName, SocketValue? value = null)
        => new(name, typeName, false, false, value);
}
