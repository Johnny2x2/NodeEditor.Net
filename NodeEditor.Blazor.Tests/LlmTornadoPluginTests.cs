using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using LlmTornado.Chat;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services.Execution;
using NodeEditor.Blazor.Services;
using NodeEditor.Blazor.Services.Registry;
using NodeEditor.Plugins.LlmTornado;
using Xunit;

namespace NodeEditor.Blazor.Tests;

public sealed class LlmTornadoPluginTests
{
    [Fact]
    public void PluginRegistersCoreNodes()
    {
        var discovery = new NodeDiscoveryService();
        var registry = new NodeRegistryService(discovery);
        var plugin = new LlmTornadoPlugin();

        plugin.Register(registry);

        var definitions = registry.Definitions;
        Assert.Contains(definitions, definition => definition.Name == "Create Agent");
        Assert.Contains(definitions, definition => definition.Name == "Run Agent");
        Assert.Contains(definitions, definition => definition.Name == "MCP Initialize");
        Assert.Contains(definitions, definition => definition.Name == "Create Image Part");
        Assert.Contains(definitions, definition => definition.Name == "Invoke Orchestration");
    }

    [Fact]
    public void AsyncNodesExposeTaskOutputs()
    {
        var methods = typeof(LlmTornadoNodeContext)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

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
        var service = CreateExecutor();
        var nodeContext = new LlmTornadoNodeContext();

        await service.ExecuteAsync(nodes, connections, context, nodeContext, PlannedOptions, CancellationToken.None);

        Assert.Equal("Hello from nodes", context.GetSocketValue("extract", "Text"));
    }

    [Fact]
    public async Task PartNodes_CreateParts_PopulatesOutputs()
    {
        var nodes = new List<NodeData>
        {
            CreateTextPartNode("textPart", "Hello"),
            CreateImagePartNode("imagePart", "https://example.com/image.png")
        };

        var context = new NodeExecutionContext();
        var service = CreateExecutor();
        var nodeContext = new LlmTornadoNodeContext();

        await service.ExecuteAsync(nodes, Array.Empty<ConnectionData>(), context, nodeContext, PlannedOptions, CancellationToken.None);

        var textPart = context.GetSocketValue("textPart", "Part");
        var imagePart = context.GetSocketValue("imagePart", "Part");

        Assert.IsType<ChatMessagePart>(textPart);
        Assert.IsType<ChatMessagePart>(imagePart);
    }

    [Fact]
    public async Task UserMessageFromParts_NodeBuildsMessage()
    {
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

        var service = CreateExecutor();
        var nodeContext = new LlmTornadoNodeContext();

        await service.ExecuteAsync(nodes, Array.Empty<ConnectionData>(), context, nodeContext, PlannedOptions, CancellationToken.None);

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

    private static NodeExecutionService CreateExecutor()
        => new(new ExecutionPlanner(), new SocketTypeResolver());

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
