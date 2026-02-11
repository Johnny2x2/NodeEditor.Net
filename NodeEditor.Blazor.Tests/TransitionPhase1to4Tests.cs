using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Blazor.Tests;

/// <summary>
/// Verification tests for the transition plan Phases 1–4.
/// These tests validate that all Phase 1A, 1B, 2A, 3A, 4A, 4B deliverables
/// are correctly implemented and match the specification.
/// </summary>
public sealed class TransitionPhase1to4Tests
{
    // ═══════════════════════════════════════════════════════════════
    // Phase 1A — Utility Types
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Phase1A_ExecutionSocket_TypeName_IsFullyQualified()
    {
        Assert.Equal("NodeEditor.Net.Services.Execution.ExecutionSocket", ExecutionSocket.TypeName);
    }

    [Fact]
    public void Phase1A_StreamMode_HasSequentialAndFireAndForget()
    {
        var values = Enum.GetValues<StreamMode>();
        Assert.Contains(StreamMode.Sequential, values);
        Assert.Contains(StreamMode.FireAndForget, values);
        Assert.Equal(2, values.Length);
    }

    [Fact]
    public void Phase1A_StreamSocketInfo_HoldsThreeFields()
    {
        var info = new StreamSocketInfo("ItemData", "OnItem", "Completed");
        Assert.Equal("ItemData", info.ItemDataSocket);
        Assert.Equal("OnItem", info.OnItemExecSocket);
        Assert.Equal("Completed", info.CompletedExecSocket);
    }

    [Fact]
    public void Phase1A_StreamSocketInfo_CompletedCanBeNull()
    {
        var info = new StreamSocketInfo("ItemData", "OnItem", null);
        Assert.Null(info.CompletedExecSocket);
    }

    // ═══════════════════════════════════════════════════════════════
    // Phase 1B — Core Interfaces (NodeBase, INodeExecutionContext, INodeRuntimeStorage)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Phase1B_NodeBase_IsAbstractWithRequiredMembers()
    {
        var type = typeof(NodeBase);
        Assert.True(type.IsAbstract);

        // Configure and ExecuteAsync must be abstract
        var configure = type.GetMethod("Configure");
        Assert.NotNull(configure);
        Assert.True(configure!.IsAbstract);

        var executeAsync = type.GetMethod("ExecuteAsync");
        Assert.NotNull(executeAsync);
        Assert.True(executeAsync!.IsAbstract);

        // OnCreatedAsync and OnDisposed must be virtual (not abstract)
        var onCreated = type.GetMethod("OnCreatedAsync");
        Assert.NotNull(onCreated);
        Assert.True(onCreated!.IsVirtual);
        Assert.False(onCreated.IsAbstract);

        var onDisposed = type.GetMethod("OnDisposed");
        Assert.NotNull(onDisposed);
        Assert.True(onDisposed!.IsVirtual);
        Assert.False(onDisposed.IsAbstract);
    }

    [Fact]
    public void Phase1B_NodeBase_HasNodeIdProperty()
    {
        var prop = typeof(NodeBase).GetProperty("NodeId");
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
        Assert.True(prop.CanRead);
    }

    [Fact]
    public void Phase1B_INodeExecutionContext_HasAllMembers()
    {
        var type = typeof(INodeExecutionContext);
        Assert.True(type.IsInterface);

        // Properties
        Assert.NotNull(type.GetProperty("Node"));
        Assert.NotNull(type.GetProperty("Services"));
        Assert.NotNull(type.GetProperty("CancellationToken"));
        Assert.NotNull(type.GetProperty("EventBus"));
        Assert.NotNull(type.GetProperty("RuntimeStorage"));

        // Data I/O — use name-only search since overloads cause AmbiguousMatchException
        var getInputMethods = type.GetMethods().Where(m => m.Name == "GetInput").ToList();
        Assert.True(getInputMethods.Count >= 2, "Expected generic and non-generic GetInput overloads");

        var setOutputMethods = type.GetMethods().Where(m => m.Name == "SetOutput").ToList();
        Assert.True(setOutputMethods.Count >= 2, "Expected generic and non-generic SetOutput overloads");

        // Execution flow
        Assert.NotNull(type.GetMethod("TriggerAsync"));

        // Streaming
        var emitMethods = type.GetMethods().Where(m => m.Name == "EmitAsync").ToList();
        Assert.True(emitMethods.Count >= 1);

        // Variables
        Assert.NotNull(type.GetMethod("GetVariable"));
        Assert.NotNull(type.GetMethod("SetVariable"));

        // Feedback
        Assert.NotNull(type.GetMethod("EmitFeedback"));
    }

    [Fact]
    public void Phase1B_INodeRuntimeStorage_HasAllMembers()
    {
        var type = typeof(INodeRuntimeStorage);
        Assert.True(type.IsInterface);

        Assert.NotNull(type.GetMethod("TryGetSocketValue"));
        Assert.NotNull(type.GetMethod("GetSocketValue"));
        Assert.NotNull(type.GetMethod("SetSocketValue"));
        Assert.NotNull(type.GetMethod("IsNodeExecuted"));
        Assert.NotNull(type.GetMethod("MarkNodeExecuted"));
        Assert.NotNull(type.GetMethod("ClearNodeExecuted"));
        Assert.NotNull(type.GetMethod("GetVariable"));
        Assert.NotNull(type.GetMethod("SetVariable"));
        Assert.NotNull(type.GetProperty("CurrentGeneration"));
        Assert.NotNull(type.GetMethod("PushGeneration"));
        Assert.NotNull(type.GetMethod("PopGeneration"));
        Assert.NotNull(type.GetMethod("ClearExecutedForNodes"));
        Assert.NotNull(type.GetMethod("CreateChild"));
        Assert.NotNull(type.GetProperty("EventBus"));
    }

    [Fact]
    public void Phase1B_NodeExecutionContext_ImplementsINodeRuntimeStorage()
    {
        var context = new NodeExecutionContext();
        Assert.IsAssignableFrom<INodeRuntimeStorage>(context);
    }

    // ═══════════════════════════════════════════════════════════════
    // Phase 2A — NodeBuilder fluent API
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Phase2A_NodeBuilder_Create_ProducesValidDefinition()
    {
        var builder = NodeBuilder.Create("TestNode");
        builder.Category("TestCategory");
        builder.Description("A test node");
        builder.Input<int>("A");
        builder.Output<int>("B");
        builder.OnExecute((ctx, ct) =>
        {
            ctx.SetOutput("B", ctx.GetInput<int>("A") * 2);
            return Task.CompletedTask;
        });
        var definition = builder.Build();

        Assert.Equal("TestNode", definition.Name);
        Assert.Equal("TestCategory", definition.Category);
        Assert.Equal("A test node", definition.Description);
        Assert.Single(definition.Inputs, s => s.Name == "A");
        Assert.Single(definition.Outputs, s => s.Name == "B");
        Assert.NotNull(definition.InlineExecutor);
        Assert.Null(definition.NodeType);
    }

    [Fact]
    public void Phase2A_NodeBuilder_Callable_AddsEnterAndExit()
    {
        var builder = NodeBuilder.Create("CallableNode");
        builder.Callable();
        var definition = builder.Build();

        Assert.Contains(definition.Inputs, s => s.Name == "Enter" && s.IsExecution);
        Assert.Contains(definition.Outputs, s => s.Name == "Exit" && s.IsExecution);
    }

    [Fact]
    public void Phase2A_NodeBuilder_ExecutionInitiator_AddsOnlyExit()
    {
        var builder = NodeBuilder.Create("InitNode");
        builder.ExecutionInitiator();
        var definition = builder.Build();

        Assert.DoesNotContain(definition.Inputs, s => s.Name == "Enter");
        Assert.Contains(definition.Outputs, s => s.Name == "Exit" && s.IsExecution);
    }

    [Fact]
    public void Phase2A_NodeBuilder_StreamOutput_AddsThreeSockets()
    {
        var builder = NodeBuilder.Create("StreamNode");
        builder.StreamOutput<string>("Item", "OnItem", "Completed");
        var definition = builder.Build();

        Assert.Contains(definition.Outputs, s => s.Name == "Item" && !s.IsExecution);
        Assert.Contains(definition.Outputs, s => s.Name == "OnItem" && s.IsExecution);
        Assert.Contains(definition.Outputs, s => s.Name == "Completed" && s.IsExecution);
        Assert.NotNull(definition.StreamSockets);
        Assert.Single(definition.StreamSockets!);
        Assert.Equal("Item", definition.StreamSockets![0].ItemDataSocket);
        Assert.Equal("OnItem", definition.StreamSockets[0].OnItemExecSocket);
        Assert.Equal("Completed", definition.StreamSockets[0].CompletedExecSocket);
    }

    [Fact]
    public void Phase2A_NodeBuilder_StreamOutput_WithoutCompleted()
    {
        var builder = NodeBuilder.Create("StreamNoComplete");
        builder.StreamOutput<int>("Value", "OnValue", completedExecName: null);
        var definition = builder.Build();

        Assert.Contains(definition.Outputs, s => s.Name == "Value" && !s.IsExecution);
        Assert.Contains(definition.Outputs, s => s.Name == "OnValue" && s.IsExecution);
        Assert.DoesNotContain(definition.Outputs, s => s.Name == "Completed");
        Assert.Null(definition.StreamSockets![0].CompletedExecSocket);
    }

    [Fact]
    public void Phase2A_NodeBuilder_CreateForType_SetsNodeType()
    {
        // CreateForType is internal, so we test it indirectly via discovery
        var discovery = new NodeDiscoveryService();
        var definition = discovery.BuildDefinitionFromType(typeof(SampleTestNode));

        Assert.NotNull(definition);
        Assert.Equal(typeof(SampleTestNode), definition!.NodeType);
        Assert.Contains(typeof(SampleTestNode).FullName!, definition.Id);
    }

    [Fact]
    public void Phase2A_NodeBuilder_Build_ProducesWorkingFactory()
    {
        var builder = NodeBuilder.Create("FactoryTest");
        builder.Input<int>("X");
        builder.Output<int>("Y");
        var definition = builder.Build();

        var nodeData = definition.Factory();

        Assert.NotNull(nodeData);
        Assert.Equal("FactoryTest", nodeData.Name);
        Assert.Equal(definition.Id, nodeData.DefinitionId);
        Assert.Single(nodeData.Inputs, s => s.Name == "X");
        Assert.Single(nodeData.Outputs, s => s.Name == "Y");
        Assert.NotEmpty(nodeData.Id);

        // Factory should produce unique IDs
        var nodeData2 = definition.Factory();
        Assert.NotEqual(nodeData.Id, nodeData2.Id);
    }

    [Fact]
    public void Phase2A_NodeBuilder_DuplicateSocketsAreIgnored()
    {
        var builder = NodeBuilder.Create("DedupTest");
        builder.Callable();
        builder.Callable(); // duplicate
        builder.Input<int>("A");
        builder.Input<int>("A"); // duplicate
        builder.Output<int>("B");
        builder.Output<int>("B"); // duplicate
        var definition = builder.Build();

        Assert.Equal(1, definition.Inputs.Count(s => s.Name == "Enter"));
        Assert.Equal(1, definition.Inputs.Count(s => s.Name == "A"));
        Assert.Equal(1, definition.Outputs.Count(s => s.Name == "Exit"));
        Assert.Equal(1, definition.Outputs.Count(s => s.Name == "B"));
    }

    [Fact]
    public void Phase2A_NodeBuilder_ExecutionInput_AddsNamedInput()
    {
        var builder = NodeBuilder.Create("MultiExec");
        builder.ExecutionInput("Reset");
        builder.ExecutionOutput("Done");
        var definition = builder.Build();

        Assert.Contains(definition.Inputs, s => s.Name == "Reset" && s.IsExecution);
        Assert.Contains(definition.Outputs, s => s.Name == "Done" && s.IsExecution);
    }

    [Fact]
    public void Phase2A_NodeBuilder_UntypedInputOutput()
    {
        var builder = NodeBuilder.Create("UntypedTest");
        builder.Input("Foo", "System.String", defaultValue: null);
        builder.Output("Bar", "System.Int32");
        var definition = builder.Build();

        Assert.Contains(definition.Inputs, s => s.Name == "Foo" && s.TypeName == "System.String");
        Assert.Contains(definition.Outputs, s => s.Name == "Bar" && s.TypeName == "System.Int32");
    }

    // ═══════════════════════════════════════════════════════════════
    // Phase 3A — NodeDefinition Extension
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Phase3A_NodeDefinition_BackwardCompatible_SevenArgs()
    {
        // Old callers passing only 7 positional args should still compile and work
        var def = new NodeDefinition(
            "test-id",
            "TestName",
            "TestCategory",
            "TestDescription",
            Array.Empty<SocketData>(),
            Array.Empty<SocketData>(),
            () => new NodeData("1", "TestName", false, false,
                Array.Empty<SocketData>(), Array.Empty<SocketData>()));

        Assert.Null(def.NodeType);
        Assert.Null(def.InlineExecutor);
        Assert.Null(def.StreamSockets);
    }

    [Fact]
    public void Phase3A_NodeDefinition_AllTenFields()
    {
        var executor = (INodeExecutionContext ctx, CancellationToken ct) => Task.CompletedTask;
        var streams = new List<StreamSocketInfo> { new("Item", "OnItem", "Done") }.AsReadOnly();

        var def = new NodeDefinition(
            Id: "full-id",
            Name: "FullNode",
            Category: "Cat",
            Description: "Desc",
            Inputs: Array.Empty<SocketData>(),
            Outputs: Array.Empty<SocketData>(),
            Factory: () => new NodeData("1", "FullNode", false, false,
                Array.Empty<SocketData>(), Array.Empty<SocketData>()),
            NodeType: typeof(SampleTestNode),
            InlineExecutor: executor,
            StreamSockets: streams);

        Assert.Equal(typeof(SampleTestNode), def.NodeType);
        Assert.Same(executor, def.InlineExecutor);
        Assert.Same(streams, def.StreamSockets);
    }

    // ═══════════════════════════════════════════════════════════════
    // Phase 4A — NodeDiscoveryService (NodeBase scanning)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Phase4A_DiscoveryService_FindsNodeBaseSubclasses()
    {
        var discovery = new NodeDiscoveryService();
        var definitions = discovery.DiscoverFromAssemblies(new[] { typeof(SampleTestNode).Assembly });

        Assert.Contains(definitions, d =>
            d.NodeType == typeof(SampleTestNode) && d.Name == "SampleTest");
    }

    [Fact]
    public void Phase4A_DiscoveryService_SkipsAbstractNodeBase()
    {
        var discovery = new NodeDiscoveryService();
        var definitions = discovery.DiscoverFromAssemblies(new[] { typeof(AbstractTestNode).Assembly });

        Assert.DoesNotContain(definitions, d => d.NodeType == typeof(AbstractTestNode));
    }

    [Fact]
    public void Phase4A_DiscoveryService_SkipsNoParameterlessConstructor()
    {
        var discovery = new NodeDiscoveryService();
        var definitions = discovery.DiscoverFromAssemblies(new[] { typeof(NoDefaultCtorNode).Assembly });

        Assert.DoesNotContain(definitions, d => d.NodeType == typeof(NoDefaultCtorNode));
    }

    [Fact]
    public void Phase4A_BuildDefinitionFromType_ReturnsNull_ForAbstract()
    {
        var discovery = new NodeDiscoveryService();
        var result = discovery.BuildDefinitionFromType(typeof(AbstractTestNode));
        Assert.Null(result);
    }

    [Fact]
    public void Phase4A_BuildDefinitionFromType_CallsConfigureAndBuilds()
    {
        var discovery = new NodeDiscoveryService();
        var definition = discovery.BuildDefinitionFromType(typeof(SampleTestNode));

        Assert.NotNull(definition);
        Assert.Equal("SampleTest", definition!.Name);
        Assert.Equal("Test", definition.Category);
        Assert.Contains(definition.Inputs, s => s.Name == "Enter" && s.IsExecution);
        Assert.Contains(definition.Inputs, s => s.Name == "Value" && !s.IsExecution);
        Assert.Contains(definition.Outputs, s => s.Name == "Exit" && s.IsExecution);
        Assert.Contains(definition.Outputs, s => s.Name == "Result" && !s.IsExecution);
        Assert.Equal(typeof(SampleTestNode), definition.NodeType);
    }

    [Fact]
    public void Phase4A_DiscoveryService_StillFindsOldINodeContextTypes()
    {
        // The hybrid discovery service should still discover old-style INodeContext nodes
        // so existing functionality is preserved during transition
        var discovery = new NodeDiscoveryService();
        var definitions = discovery.DiscoverFromAssemblies(new[] { typeof(OldStyleTestContext).Assembly });

        Assert.Contains(definitions, d => d.Name == "OldStyleNode");
    }

    // ═══════════════════════════════════════════════════════════════
    // Phase 4B — NodeRegistryService
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Phase4B_RegistryService_RegistersNodeBaseTypes()
    {
        var discovery = new NodeDiscoveryService();
        var registry = new NodeRegistryService(discovery);
        registry.RegisterFromAssembly(typeof(SampleTestNode).Assembly);

        Assert.Contains(registry.Definitions, d =>
            d.NodeType == typeof(SampleTestNode) && d.Name == "SampleTest");
    }

    [Fact]
    public void Phase4B_RegistryService_Deduplicates()
    {
        var discovery = new NodeDiscoveryService();
        var registry = new NodeRegistryService(discovery);

        registry.RegisterFromAssembly(typeof(SampleTestNode).Assembly);
        var count1 = registry.Definitions.Count;

        registry.RegisterFromAssembly(typeof(SampleTestNode).Assembly);
        var count2 = registry.Definitions.Count;

        Assert.Equal(count1, count2);
    }

    [Fact]
    public void Phase4B_RegistryService_CatalogSearchFindsNewNodes()
    {
        var discovery = new NodeDiscoveryService();
        var registry = new NodeRegistryService(discovery);
        registry.RegisterFromAssembly(typeof(SampleTestNode).Assembly);

        var catalog = registry.GetCatalog("SampleTest");

        Assert.Contains(catalog.All, d => d.Name == "SampleTest");
    }

    [Fact]
    public void Phase4B_RegistryService_RegistersInlineDefinitions()
    {
        var discovery = new NodeDiscoveryService();
        var registry = new NodeRegistryService(discovery);

        // Force initialization
        _ = registry.Definitions;

        // StandardNodeRegistration.GetInlineDefinitions() returns empty now (Phase 8),
        // but the call path is wired — verify no crash
        Assert.NotNull(registry.Definitions);
    }

    [Fact]
    public void Phase4B_RegistryService_RemoveDefinitions()
    {
        var discovery = new NodeDiscoveryService();
        var registry = new NodeRegistryService(discovery);
        registry.RegisterFromAssembly(typeof(SampleTestNode).Assembly);

        var before = registry.Definitions.Count;
        var toRemove = registry.Definitions.Where(d => d.NodeType == typeof(SampleTestNode)).ToList();
        var removed = registry.RemoveDefinitions(toRemove);

        Assert.True(removed > 0);
        Assert.True(registry.Definitions.Count < before);
    }

    // ═══════════════════════════════════════════════════════════════
    // Cross-phase integration: Builder → Definition → Discovery → Registry
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Integration_NodeBase_DiscoveredAndRegistered_EndToEnd()
    {
        // A NodeBase subclass should be discoverable, produce a valid definition,
        // and be registerable — the full Phase 1→4 pipeline
        var discovery = new NodeDiscoveryService();
        var registry = new NodeRegistryService(discovery);
        registry.RegisterFromAssembly(typeof(SampleTestNode).Assembly);

        var definition = registry.Definitions.FirstOrDefault(d => d.NodeType == typeof(SampleTestNode));
        Assert.NotNull(definition);

        // Definition has correct metadata from Configure()
        Assert.Equal("SampleTest", definition!.Name);
        Assert.Equal("Test", definition.Category);

        // Factory produces valid NodeData
        var nodeData = definition.Factory();
        Assert.NotNull(nodeData);
        Assert.Equal("SampleTest", nodeData.Name);

        // NodeData has proper sockets
        Assert.Contains(nodeData.Inputs, s => s.Name == "Enter");
        Assert.Contains(nodeData.Inputs, s => s.Name == "Value");
        Assert.Contains(nodeData.Outputs, s => s.Name == "Exit");
        Assert.Contains(nodeData.Outputs, s => s.Name == "Result");
    }

    [Fact]
    public void Integration_InlineNode_BuildsAndRegisters()
    {
        var inlineBuilder = NodeBuilder.Create("InlineAdd");
        inlineBuilder.Category("Math");
        inlineBuilder.Description("Adds two numbers");
        inlineBuilder.Input<int>("A");
        inlineBuilder.Input<int>("B");
        inlineBuilder.Output<int>("Sum");
        inlineBuilder.OnExecute((ctx, ct) =>
        {
            var a = ctx.GetInput<int>("A");
            var b = ctx.GetInput<int>("B");
            ctx.SetOutput("Sum", a + b);
            return Task.CompletedTask;
        });
        var inlineDef = inlineBuilder.Build();

        Assert.Equal("InlineAdd", inlineDef.Id);
        Assert.NotNull(inlineDef.InlineExecutor);
        Assert.Null(inlineDef.NodeType);
        Assert.Equal(2, inlineDef.Inputs.Count);
        Assert.Single(inlineDef.Outputs);

        // Can register it
        var discovery = new NodeDiscoveryService();
        var registry = new NodeRegistryService(discovery);
        registry.RegisterDefinitions(new List<NodeDefinition> { inlineDef });

        Assert.Contains(registry.Definitions, d => d.Id == "InlineAdd");
    }

    // ═══════════════════════════════════════════════════════════════
    // Test fixtures — NodeBase subclasses for discovery testing  
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Concrete NodeBase subclass for testing Phase 1-4 pipeline.</summary>
    public sealed class SampleTestNode : NodeBase
    {
        public override void Configure(INodeBuilder builder)
        {
            builder
                .Name("SampleTest")
                .Category("Test")
                .Description("A sample test node")
                .Callable()
                .Input<int>("Value")
                .Output<int>("Result");
        }

        public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
        {
            var value = context.GetInput<int>("Value");
            context.SetOutput("Result", value * 2);
            return Task.CompletedTask;
        }
    }

    /// <summary>Abstract node — should be skipped by discovery.</summary>
    public abstract class AbstractTestNode : NodeBase
    {
    }

    /// <summary>Node without parameterless constructor — should be skipped by discovery.</summary>
    public sealed class NoDefaultCtorNode : NodeBase
    {
        private readonly string _required;

        public NoDefaultCtorNode(string required)
        {
            _required = required;
        }

        public override void Configure(INodeBuilder builder)
        {
            builder.Name("NoDefaultCtor").Category("Test");
        }

        public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
            => Task.CompletedTask;
    }

    /// <summary>Old-style INodeContext for verifying hybrid discovery still works.</summary>
    private sealed class OldStyleTestContext : INodeContext
    {
        [Node("OldStyleNode", category: "Legacy", description: "Tests backward compat")]
        public void OldStyleNode(out int Value)
        {
            Value = 42;
        }
    }
}
