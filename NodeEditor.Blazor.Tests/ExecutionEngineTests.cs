using System.Collections.Concurrent;
using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Blazor.Tests;

public sealed class ExecutionEngineTests
{
    private static NodeExecutionService CreateService()
    {
        var registry = new NodeRegistryService(new NodeDiscoveryService());
        registry.EnsureInitialized();
        return new NodeExecutionService(new ExecutionPlanner(), registry, new MinimalServiceProvider());
    }

    private static NodeExecutionService CreateService(out NodeRegistryService registry)
    {
        registry = new NodeRegistryService(new NodeDiscoveryService());
        registry.EnsureInitialized();
        return new NodeExecutionService(new ExecutionPlanner(), registry, new MinimalServiceProvider());
    }

    private static NodeData NodeFromDef(NodeRegistryService registry, string defName, string id)
    {
        var def = registry.Definitions.First(d => d.Name == defName && (d.NodeType is not null || d.InlineExecutor is not null));
        return def.Factory() with { Id = id };
    }

    private static NodeData NodeFromDef(NodeRegistryService registry, string defName, string id, params (string socketName, object value)[] inputOverrides)
    {
        var def = registry.Definitions.First(d => d.Name == defName && (d.NodeType is not null || d.InlineExecutor is not null));
        var node = def.Factory() with { Id = id };
        if (inputOverrides.Length == 0) return node;

        var newInputs = node.Inputs.Select(s =>
        {
            var over = inputOverrides.FirstOrDefault(o => o.socketName == s.Name);
            if (over != default)
                return s with { Value = SocketValue.FromObject(over.value) };
            return s;
        }).ToArray();

        return node with { Inputs = newInputs };
    }

    // â”€â”€ Sequential Execution â”€â”€

    [Fact]
    public async Task SequentialExecution_StartTriggersDownstream()
    {
        var service = CreateService(out var registry);
        var start = NodeFromDef(registry, "Start", "start");
        var marker = NodeFromDef(registry, "Marker", "marker");

        var nodes = new List<NodeData> { start, marker };
        var connections = new List<ConnectionData>
        {
            TestConnections.Exec("start", "Exit", "marker", "Enter")
        };

        var context = new NodeRuntimeStorage();
        await service.ExecuteAsync(nodes, connections, context, null!, NodeExecutionOptions.Default, CancellationToken.None);

        Assert.True(context.IsNodeExecuted("start"));
        Assert.True(context.IsNodeExecuted("marker"));
    }

    [Fact]
    public async Task SequentialExecution_FollowsSignaledBranch_True()
    {
        var service = CreateService(out var registry);
        var start = NodeFromDef(registry, "Start", "start");
        var branch = NodeFromDef(registry, "Branch", "branch", ("Cond", true));
        var trueMarker = NodeFromDef(registry, "Marker", "trueMarker");
        var falseMarker = NodeFromDef(registry, "Marker", "falseMarker");

        var nodes = new List<NodeData> { start, branch, trueMarker, falseMarker };
        var connections = new List<ConnectionData>
        {
            TestConnections.Exec("start", "Exit", "branch", "Start"),
            TestConnections.Exec("branch", "True", "trueMarker", "Enter"),
            TestConnections.Exec("branch", "False", "falseMarker", "Enter")
        };

        var context = new NodeRuntimeStorage();
        await service.ExecuteAsync(nodes, connections, context, null!, NodeExecutionOptions.Default, CancellationToken.None);

        Assert.True(context.IsNodeExecuted("trueMarker"), "True branch should execute");
        Assert.False(context.IsNodeExecuted("falseMarker"), "False branch should NOT execute");
    }

    [Fact]
    public async Task SequentialExecution_FollowsSignaledBranch_False()
    {
        var service = CreateService(out var registry);
        var start = NodeFromDef(registry, "Start", "start");
        var branch = NodeFromDef(registry, "Branch", "branch", ("Cond", false));
        var trueMarker = NodeFromDef(registry, "Marker", "trueMarker");
        var falseMarker = NodeFromDef(registry, "Marker", "falseMarker");

        var nodes = new List<NodeData> { start, branch, trueMarker, falseMarker };
        var connections = new List<ConnectionData>
        {
            TestConnections.Exec("start", "Exit", "branch", "Start"),
            TestConnections.Exec("branch", "True", "trueMarker", "Enter"),
            TestConnections.Exec("branch", "False", "falseMarker", "Enter")
        };

        var context = new NodeRuntimeStorage();
        await service.ExecuteAsync(nodes, connections, context, null!, NodeExecutionOptions.Default, CancellationToken.None);

        Assert.False(context.IsNodeExecuted("trueMarker"), "True branch should NOT execute");
        Assert.True(context.IsNodeExecuted("falseMarker"), "False branch should execute");
    }

    // â”€â”€ Data Resolution â”€â”€

    [Fact]
    public async Task DataNode_ResolvesUpstreamLazily()
    {
        var service = CreateService(out var registry);
        var start = NodeFromDef(registry, "Start", "start");
        var abs = NodeFromDef(registry, "Abs", "abs", ("Value", -5.0));
        var consume = NodeFromDef(registry, "Consume", "consume");

        var nodes = new List<NodeData> { start, abs, consume };
        var connections = new List<ConnectionData>
        {
            TestConnections.Exec("start", "Exit", "consume", "Enter"),
            TestConnections.Data("abs", "Result", "consume", "Value")
        };

        var context = new NodeRuntimeStorage();
        await service.ExecuteAsync(nodes, connections, context, null!, NodeExecutionOptions.Default, CancellationToken.None);

        Assert.True(context.IsNodeExecuted("abs"), "Data node should have been lazily evaluated");
        Assert.Equal(5.0, context.GetSocketValue("abs", "Result"));
    }

    [Fact]
    public async Task DataPipeline_AbsToClamp()
    {
        var service = CreateService(out var registry);
        var start = NodeFromDef(registry, "Start", "start");
        var abs = NodeFromDef(registry, "Abs", "abs", ("Value", -15.0));
        var clamp = NodeFromDef(registry, "Clamp", "clamp", ("Min", 0.0), ("Max", 10.0));
        var consume = NodeFromDef(registry, "Consume", "consume");

        var nodes = new List<NodeData> { start, abs, clamp, consume };
        var connections = new List<ConnectionData>
        {
            TestConnections.Exec("start", "Exit", "consume", "Enter"),
            TestConnections.Data("abs", "Result", "clamp", "Value"),
            TestConnections.Data("clamp", "Result", "consume", "Value")
        };

        var context = new NodeRuntimeStorage();
        await service.ExecuteAsync(nodes, connections, context, null!, NodeExecutionOptions.Default, CancellationToken.None);

        Assert.Equal(15.0, context.GetSocketValue("abs", "Result"));
        Assert.Equal(10.0, context.GetSocketValue("clamp", "Result"));
    }

    // â”€â”€ Loops â”€â”€

    [Fact]
    public async Task ForLoop_IteratesCorrectTimes()
    {
        var service = CreateService(out var registry);
        var start = NodeFromDef(registry, "Start", "start");
        var loop = NodeFromDef(registry, "For Loop", "loop", ("LoopTimes", 3));
        var marker = NodeFromDef(registry, "Marker", "body");
        var end = NodeFromDef(registry, "Marker", "end");

        var nodes = new List<NodeData> { start, loop, marker, end };
        var connections = new List<ConnectionData>
        {
            TestConnections.Exec("start", "Exit", "loop", "Enter"),
            TestConnections.Exec("loop", "LoopPath", "body", "Enter"),
            TestConnections.Exec("loop", "Exit", "end", "Enter")
        };

        var context = new NodeRuntimeStorage();
        await service.ExecuteAsync(nodes, connections, context, null!, NodeExecutionOptions.Default, CancellationToken.None);

        Assert.True(context.IsNodeExecuted("body"), "Loop body should have executed");
        Assert.True(context.IsNodeExecuted("end"), "Exit marker should have been reached");
        // Last index should be 2 (0, 1, 2)
        Assert.Equal(2, context.GetSocketValue("loop", "Index"));
    }

    [Fact]
    public async Task ForLoopStep_IteratesWithStep()
    {
        var service = CreateService(out var registry);
        var start = NodeFromDef(registry, "Start", "start");
        var loop = NodeFromDef(registry, "For Loop Step", "loop", ("StartValue", 0), ("EndValue", 6), ("Step", 2));
        var end = NodeFromDef(registry, "Marker", "end");

        var nodes = new List<NodeData> { start, loop, end };
        var connections = new List<ConnectionData>
        {
            TestConnections.Exec("start", "Exit", "loop", "Enter"),
            TestConnections.Exec("loop", "Exit", "end", "Enter")
        };

        var context = new NodeRuntimeStorage();
        await service.ExecuteAsync(nodes, connections, context, null!, NodeExecutionOptions.Default, CancellationToken.None);

        Assert.True(context.IsNodeExecuted("end"), "Exit should have been reached");
        // Iterates: 0, 2, 4 â†’ last Index = 4
        Assert.Equal(4, context.GetSocketValue("loop", "Index"));
    }

    [Fact]
    public async Task WhileLoop_FalseCondition_ExitsImmediately()
    {
        var service = CreateService(out var registry);
        var start = NodeFromDef(registry, "Start", "start");
        var loop = NodeFromDef(registry, "While Loop", "loop", ("Condition", false));
        var body = NodeFromDef(registry, "Marker", "body");
        var end = NodeFromDef(registry, "Marker", "end");

        var nodes = new List<NodeData> { start, loop, body, end };
        var connections = new List<ConnectionData>
        {
            TestConnections.Exec("start", "Exit", "loop", "Enter"),
            TestConnections.Exec("loop", "LoopPath", "body", "Enter"),
            TestConnections.Exec("loop", "Exit", "end", "Enter")
        };

        var context = new NodeRuntimeStorage();
        await service.ExecuteAsync(nodes, connections, context, null!, NodeExecutionOptions.Default, CancellationToken.None);

        Assert.False(context.IsNodeExecuted("body"), "Body should NOT execute when condition is false");
        Assert.True(context.IsNodeExecuted("end"), "Exit should be reached");
    }

    [Fact]
    public async Task DoWhileLoop_ExecutesAtLeastOnce()
    {
        var service = CreateService(out var registry);
        var start = NodeFromDef(registry, "Start", "start");
        var loop = NodeFromDef(registry, "Do While Loop", "loop", ("Condition", false));
        var body = NodeFromDef(registry, "Marker", "body");
        var end = NodeFromDef(registry, "Marker", "end");

        var nodes = new List<NodeData> { start, loop, body, end };
        var connections = new List<ConnectionData>
        {
            TestConnections.Exec("start", "Exit", "loop", "Enter"),
            TestConnections.Exec("loop", "LoopPath", "body", "Enter"),
            TestConnections.Exec("loop", "Exit", "end", "Enter")
        };

        var context = new NodeRuntimeStorage();
        await service.ExecuteAsync(nodes, connections, context, null!, NodeExecutionOptions.Default, CancellationToken.None);

        Assert.True(context.IsNodeExecuted("body"), "Body should execute at least once in do-while");
        Assert.True(context.IsNodeExecuted("end"), "Exit should be reached");
    }

    // â”€â”€ Cancellation â”€â”€

    [Fact]
    public async Task Cancellation_StopsExecutionWithinTimeout()
    {
        var service = CreateService(out var registry);
        var start = NodeFromDef(registry, "Start", "start");
        var delay = NodeFromDef(registry, "Delay", "delay", ("DelayMs", 5000));

        var nodes = new List<NodeData> { start, delay };
        var connections = new List<ConnectionData>
        {
            TestConnections.Exec("start", "Exit", "delay", "Enter")
        };

        var context = new NodeRuntimeStorage();
        using var cts = new CancellationTokenSource(100);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ExecuteAsync(nodes, connections, context, null!, NodeExecutionOptions.Default, cts.Token));
    }

    // â”€â”€ Debug/Feedback â”€â”€

    [Fact]
    public async Task DebugPrint_EmitsFeedbackEvent()
    {
        var service = CreateService(out var registry);
        var start = NodeFromDef(registry, "Start", "start");
        var debug = NodeFromDef(registry, "Debug Print", "debug", ("Label", "test"), ("Value", (object)"hello"));

        var nodes = new List<NodeData> { start, debug };
        var connections = new List<ConnectionData>
        {
            TestConnections.Exec("start", "Exit", "debug", "Enter")
        };

        var context = new NodeRuntimeStorage();
        string? receivedMessage = null;
        service.FeedbackReceived += (_, args) => receivedMessage = args.Message;

        await service.ExecuteAsync(nodes, connections, context, null!, NodeExecutionOptions.Default, CancellationToken.None);

        Assert.NotNull(receivedMessage);
        Assert.Contains("test", receivedMessage);
        Assert.Contains("hello", receivedMessage);
    }

    [Fact]
    public async Task DebugWarning_EmitsContinueFeedback()
    {
        var service = CreateService(out var registry);
        var start = NodeFromDef(registry, "Start", "start");
        var warn = NodeFromDef(registry, "Debug Warning", "warn", ("Message", "caution!"));

        var nodes = new List<NodeData> { start, warn };
        var connections = new List<ConnectionData>
        {
            TestConnections.Exec("start", "Exit", "warn", "Enter")
        };

        var context = new NodeRuntimeStorage();
        ExecutionFeedbackType? feedbackType = null;
        service.FeedbackReceived += (_, args) => feedbackType = args.Type;

        await service.ExecuteAsync(nodes, connections, context, null!, NodeExecutionOptions.Default, CancellationToken.None);

        Assert.Equal(ExecutionFeedbackType.Continue, feedbackType);
    }

    // â”€â”€ Step Mode â”€â”€

    [Fact]
    public async Task StepMode_GatePausesAndResumes()
    {
        var service = CreateService(out var registry);
        var start = NodeFromDef(registry, "Start", "start");
        var a = NodeFromDef(registry, "Marker", "a");
        var b = NodeFromDef(registry, "Marker", "b");

        var nodes = new List<NodeData> { start, a, b };
        var connections = new List<ConnectionData>
        {
            TestConnections.Exec("start", "Exit", "a", "Enter"),
            TestConnections.Exec("a", "Exit", "b", "Enter")
        };

        var context = new NodeRuntimeStorage();
        service.Gate.StartPaused();

        var execTask = Task.Run(async () =>
            await service.ExecuteAsync(nodes, connections, context, null!, NodeExecutionOptions.Default, CancellationToken.None));

        await Task.Delay(100);
        Assert.False(context.IsNodeExecuted("start"), "Should still be paused");

        service.Gate.StepOnce();
        await Task.Delay(100);

        service.Gate.Resume();
        await execTask;

        Assert.True(context.IsNodeExecuted("start"));
        Assert.True(context.IsNodeExecuted("a"));
        Assert.True(context.IsNodeExecuted("b"));
    }

    // â”€â”€ Group Execution â”€â”€

    [Fact]
    public async Task GroupExecution_ReturnsOutputsToParentContext()
    {
        var service = CreateService(out var registry);

        // Inner group: Start â†’ Consume, with Abs(-42) lazily pulled by Consume
        var innerStart = NodeFromDef(registry, "Start", "innerStart");
        var innerAbs = NodeFromDef(registry, "Abs", "innerAbs", ("Value", -42.0));
        var innerConsume = NodeFromDef(registry, "Consume", "innerConsume");

        var innerConnections = new List<ConnectionData>
        {
            TestConnections.Exec("innerStart", "Exit", "innerConsume", "Enter"),
            TestConnections.Data("innerAbs", "Result", "innerConsume", "Value")
        };

        var group = new GroupNodeData(
            Id: "group",
            Name: "Group",
            Nodes: new List<NodeData> { innerStart, innerAbs, innerConsume },
            Connections: innerConnections,
            Inputs: Array.Empty<SocketData>(),
            Outputs: new[] { new SocketData("Value", typeof(double).FullName!, false, false) },
            InputMappings: Array.Empty<GroupSocketMapping>(),
            OutputMappings: new[] { new GroupSocketMapping("Value", "innerAbs", "Result") });

        var parentContext = new NodeRuntimeStorage();

        await service.ExecuteGroupAsync(group, parentContext, null!, NodeExecutionOptions.Default, CancellationToken.None);

        Assert.Equal(42.0, parentContext.GetSocketValue("group", "Value"));
    }

    // â”€â”€ Parallel Execution â”€â”€

    [Fact]
    public async Task ParallelInitiators_BothExecute()
    {
        var service = CreateService(out var registry);

        // Two independent Start â†’ Marker chains
        var start1 = NodeFromDef(registry, "Start", "start1");
        var marker1 = NodeFromDef(registry, "Marker", "marker1");
        var start2 = NodeFromDef(registry, "Start", "start2");
        var marker2 = NodeFromDef(registry, "Marker", "marker2");

        var nodes = new List<NodeData> { start1, marker1, start2, marker2 };
        var connections = new List<ConnectionData>
        {
            TestConnections.Exec("start1", "Exit", "marker1", "Enter"),
            TestConnections.Exec("start2", "Exit", "marker2", "Enter")
        };

        var context = new NodeRuntimeStorage();
        var options = new NodeExecutionOptions(ExecutionMode.Parallel, AllowBackground: false, MaxDegreeOfParallelism: 4);
        await service.ExecuteAsync(nodes, connections, context, null!, options, CancellationToken.None);

        Assert.True(context.IsNodeExecuted("marker1"));
        Assert.True(context.IsNodeExecuted("marker2"));
    }

    // â”€â”€ Background Queue â”€â”€

    [Fact]
    public async Task BackgroundQueue_ExecutesPlannedJobs()
    {
        var registry = new NodeRegistryService(new NodeDiscoveryService());
        registry.EnsureInitialized();
        var services = new MinimalServiceProvider();
        var service = new NodeExecutionService(new ExecutionPlanner(), registry, services);

        var start = registry.Definitions.First(d => d.Name == "Start" && d.NodeType is not null).Factory() with { Id = "start" };
        var marker = registry.Definitions.First(d => d.Name == "Marker" && d.NodeType is not null).Factory() with { Id = "marker" };

        var nodes = new List<NodeData> { start, marker };
        var connections = new List<ConnectionData>
        {
            TestConnections.Exec("start", "Exit", "marker", "Enter")
        };

        var context = new NodeRuntimeStorage();
        var queue = new BackgroundExecutionQueue();
        var worker = new BackgroundExecutionWorker(queue, service);

        var job = new ExecutionJob(Guid.NewGuid(), nodes, connections, context, null!, NodeExecutionOptions.Default);
        await queue.EnqueueAsync(job);

        using var cts = new CancellationTokenSource();
        var runTask = worker.RunAsync(cts.Token);

        var executed = await ExecutionTestHelpers.WaitUntilAsync(() => context.IsNodeExecuted("marker"), TimeSpan.FromSeconds(2));
        cts.Cancel();

        try { await runTask; } catch (OperationCanceledException) { }

        Assert.True(executed);
    }

    // â”€â”€ Event Nodes â”€â”€

    [Fact]
    public async Task StartNode_IsDiscoverableAsExecutionInitiator()
    {
        var registry = new NodeRegistryService(new NodeDiscoveryService());
        registry.EnsureInitialized();

        var startDef = registry.Definitions.First(d => d.Name == "Start" && d.NodeType is not null);
        var nodeData = startDef.Factory();

        Assert.True(nodeData.ExecInit, "Start node should be an execution initiator");
        Assert.True(nodeData.Callable, "Start node should be callable");
        Assert.Contains(nodeData.Outputs, o => o.Name == "Exit" && o.IsExecution);
    }

    [Fact]
    public async Task BranchNode_HasCorrectSocketStructure()
    {
        var registry = new NodeRegistryService(new NodeDiscoveryService());
        registry.EnsureInitialized();

        var branchDef = registry.Definitions.First(d => d.Name == "Branch" && d.NodeType is not null);
        var nodeData = branchDef.Factory();

        Assert.Contains(nodeData.Inputs, i => i.Name == "Start" && i.IsExecution);
        Assert.Contains(nodeData.Inputs, i => i.Name == "Cond" && !i.IsExecution);
        Assert.Contains(nodeData.Outputs, o => o.Name == "True" && o.IsExecution);
        Assert.Contains(nodeData.Outputs, o => o.Name == "False" && o.IsExecution);
    }

    // â”€â”€ Complex Chains â”€â”€

    [Fact]
    public async Task ComplexChain_StartBranchLoopMarker()
    {
        var service = CreateService(out var registry);
        var start = NodeFromDef(registry, "Start", "start");
        var branch = NodeFromDef(registry, "Branch", "branch", ("Cond", true));
        var loop = NodeFromDef(registry, "For Loop", "loop", ("LoopTimes", 2));
        var end = NodeFromDef(registry, "Marker", "end");

        var nodes = new List<NodeData> { start, branch, loop, end };
        var connections = new List<ConnectionData>
        {
            TestConnections.Exec("start", "Exit", "branch", "Start"),
            TestConnections.Exec("branch", "True", "loop", "Enter"),
            TestConnections.Exec("loop", "Exit", "end", "Enter")
        };

        var context = new NodeRuntimeStorage();
        await service.ExecuteAsync(nodes, connections, context, null!, NodeExecutionOptions.Default, CancellationToken.None);

        Assert.True(context.IsNodeExecuted("loop"));
        Assert.True(context.IsNodeExecuted("end"));
        Assert.Equal(1, context.GetSocketValue("loop", "Index")); // Last iteration index
    }

    [Fact]
    public async Task Delay_PausesExecution()
    {
        var service = CreateService(out var registry);
        var start = NodeFromDef(registry, "Start", "start");
        var delay = NodeFromDef(registry, "Delay", "delay", ("DelayMs", 50));
        var marker = NodeFromDef(registry, "Marker", "marker");

        var nodes = new List<NodeData> { start, delay, marker };
        var connections = new List<ConnectionData>
        {
            TestConnections.Exec("start", "Exit", "delay", "Enter"),
            TestConnections.Exec("delay", "Exit", "marker", "Enter")
        };

        var context = new NodeRuntimeStorage();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await service.ExecuteAsync(nodes, connections, context, null!, NodeExecutionOptions.Default, CancellationToken.None);
        sw.Stop();

        Assert.True(context.IsNodeExecuted("marker"));
        Assert.True(sw.ElapsedMilliseconds >= 30, $"Expected >= 30ms delay but got {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task Consume_ForcesUpstreamEvaluation()
    {
        var service = CreateService(out var registry);
        var start = NodeFromDef(registry, "Start", "start");
        var abs = NodeFromDef(registry, "Abs", "abs", ("Value", -99.0));
        var consume = NodeFromDef(registry, "Consume", "consume");

        var nodes = new List<NodeData> { start, abs, consume };
        var connections = new List<ConnectionData>
        {
            TestConnections.Exec("start", "Exit", "consume", "Enter"),
            TestConnections.Data("abs", "Result", "consume", "Value")
        };

        var context = new NodeRuntimeStorage();
        await service.ExecuteAsync(nodes, connections, context, null!, NodeExecutionOptions.Default, CancellationToken.None);

        Assert.True(context.IsNodeExecuted("abs"), "Abs should have been evaluated");
        Assert.Equal(99.0, context.GetSocketValue("abs", "Result"));
    }

    [Fact]
    public async Task RepeatUntil_ExecutesBodyThenChecksCondition()
    {
        var service = CreateService(out var registry);
        var start = NodeFromDef(registry, "Start", "start");
        // Condition starts true â†’ repeat-until (repeats until condition is true) should execute body once then exit
        var loop = NodeFromDef(registry, "Repeat Until", "loop", ("Condition", true));
        var body = NodeFromDef(registry, "Marker", "body");
        var end = NodeFromDef(registry, "Marker", "end");

        var nodes = new List<NodeData> { start, loop, body, end };
        var connections = new List<ConnectionData>
        {
            TestConnections.Exec("start", "Exit", "loop", "Enter"),
            TestConnections.Exec("loop", "LoopPath", "body", "Enter"),
            TestConnections.Exec("loop", "Exit", "end", "Enter")
        };

        var context = new NodeRuntimeStorage();
        await service.ExecuteAsync(nodes, connections, context, null!, NodeExecutionOptions.Default, CancellationToken.None);

        Assert.True(context.IsNodeExecuted("body"), "Body should execute at least once");
        Assert.True(context.IsNodeExecuted("end"), "Should exit after condition becomes true");
    }

    [Fact]
    public async Task ForEachLoop_IteratesOverList()
    {
        var service = CreateService(out var registry);
        var start = NodeFromDef(registry, "Start", "start");
        var listCreate = NodeFromDef(registry, "List Create", "list");
        var listAdd1 = NodeFromDef(registry, "List Add", "add1", ("Item", (object)"A"));
        var listAdd2 = NodeFromDef(registry, "List Add", "add2", ("Item", (object)"B"));
        var forEach = NodeFromDef(registry, "ForEach Loop", "loop");
        var end = NodeFromDef(registry, "Marker", "end");

        // Build chain: list â†’ add1 â†’ add2 â†’ forEach
        var nodes = new List<NodeData> { start, listCreate, listAdd1, listAdd2, forEach, end };
        var connections = new List<ConnectionData>
        {
            TestConnections.Exec("start", "Exit", "loop", "Enter"),
            TestConnections.Data("listCreate", "Result", "add1", "List"),
            TestConnections.Data("add1", "Result", "add2", "List"),
            TestConnections.Data("add2", "Result", "loop", "List"),
            TestConnections.Exec("loop", "Exit", "end", "Enter")
        };

        var context = new NodeRuntimeStorage();
        await service.ExecuteAsync(nodes, connections, context, null!, NodeExecutionOptions.Default, CancellationToken.None);

        Assert.True(context.IsNodeExecuted("end"), "ForEach should complete and trigger Exit");
    }

    //  Phase 13A spec-named tests (using TestGraphBuilder) 

    [Fact]
    public async Task ForLoopStepNode_HandlesNegativeStep()
    {
        var service = CreateService(out var registry);
        EnsureIncrementNodeRegistered(registry);

        var (nodes, connections) = new TestGraphBuilder()
            .AddNodeFromDefinition(registry, "Start", "start")
            .AddNodeFromDefinition(registry, "For Loop Step", "loop",
                ("StartValue", 6), ("EndValue", 0), ("Step", -2))
            .AddNodeFromDefinition(registry, "Increment", "inc", ("Key", "count"))
            .AddNodeFromDefinition(registry, "Marker", "end")
            .ConnectExecution("start", "Exit", "loop", "Enter")
            .ConnectExecution("loop", "LoopPath", "inc", "Enter")
            .ConnectExecution("loop", "Exit", "end", "Enter")
            .Build();

        var context = new NodeRuntimeStorage();
        await service.ExecuteAsync(nodes, connections, context, null!, NodeExecutionOptions.Default, CancellationToken.None);

        Assert.True(context.IsNodeExecuted("end"), "Exit should have been reached");
        Assert.Equal(2, context.GetSocketValue("loop", "Index"));
        Assert.Equal(3, context.GetVariable("count"));
    }

    [Fact]
    public async Task NestedForLoops_ExecuteCorrectly()
    {
        var service = CreateService(out var registry);
        EnsureIncrementNodeRegistered(registry);

        var (nodes, connections) = new TestGraphBuilder()
            .AddNodeFromDefinition(registry, "Start", "start")
            .AddNodeFromDefinition(registry, "For Loop", "outer", ("LoopTimes", 3))
            .AddNodeFromDefinition(registry, "For Loop", "inner", ("LoopTimes", 2))
            .AddNodeFromDefinition(registry, "Increment", "inc", ("Key", "count"))
            .AddNodeFromDefinition(registry, "Marker", "end")
            .ConnectExecution("start", "Exit", "outer", "Enter")
            .ConnectExecution("outer", "LoopPath", "inner", "Enter")
            .ConnectExecution("inner", "LoopPath", "inc", "Enter")
            .ConnectExecution("outer", "Exit", "end", "Enter")
            .Build();

        var context = new NodeRuntimeStorage();
        await service.ExecuteAsync(nodes, connections, context, null!, NodeExecutionOptions.Default, CancellationToken.None);

        Assert.True(context.IsNodeExecuted("end"));
        Assert.Equal(6, context.GetVariable("count"));
    }

    [Fact]
    public async Task VariableSetAndGet_SharesValue()
    {
        var service = CreateService(out var registry);

        var setNode = CreateSetVariableNode(nodeId: "set", variableId: "v1", value: 123);
        var getNode = CreateGetVariableNode(nodeId: "get", variableId: "v1");

        var (nodes, connections) = new TestGraphBuilder()
            .AddNodeFromDefinition(registry, "Start", "start")
            .AddNodeData(setNode)
            .AddNodeData(getNode)
            .AddNodeFromDefinition(registry, "Consume", "consume")
            .ConnectExecution("start", "Exit", "set", "Enter")
            .ConnectExecution("set", "Exit", "consume", "Enter")
            .ConnectData("get", "Value", "consume", "Value")
            .Build();

        var context = new NodeRuntimeStorage();
        await service.ExecuteAsync(nodes, connections, context, null!, NodeExecutionOptions.Default, CancellationToken.None);

        Assert.True(context.IsNodeExecuted("set"), "Set Variable node should execute");
        Assert.True(context.IsNodeExecuted("get"), "Get Variable node should be lazily executed");
        Assert.Equal(123, context.GetSocketValue("consume", "Value"));
    }

    private static void EnsureIncrementNodeRegistered(NodeRegistryService registry)
    {
        // Stable ID = "Increment" (NodeBuilder.BuildDefinitionId uses name when NodeType is null)
        var incrementDefinition = NodeBuilder.Create("Increment")
            .Category("Test")
            .Callable()
            .Input<string>("Key", "count")
            .OnExecute(async (ctx, ct) =>
            {
                var key = ctx.GetInput<string>("Key");
                var current = ctx.GetVariable(key);
                var next = current is int i ? i + 1 : 1;
                ctx.SetVariable(key, next);
                await ctx.TriggerAsync("Exit");
            })
            .Build();

        registry.RegisterDefinitions(new[] { incrementDefinition });
    }

    private static NodeData CreateSetVariableNode(string nodeId, string variableId, object value)
    {
        var defId = GraphVariable.SetDefinitionPrefix + variableId;
        var inputs = new[]
        {
            new SocketData("Enter", ExecutionSocket.TypeName, IsInput: true, IsExecution: true),
            new SocketData("Value", value.GetType().FullName ?? typeof(object).FullName!, IsInput: true, IsExecution: false, SocketValue.FromObject(value))
        };
        var outputs = new[]
        {
            new SocketData("Exit", ExecutionSocket.TypeName, IsInput: false, IsExecution: true),
            new SocketData("Value", value.GetType().FullName ?? typeof(object).FullName!, IsInput: false, IsExecution: false)
        };

        return new NodeData(
            Id: nodeId,
            Name: "Set Variable",
            Callable: true,
            ExecInit: false,
            Inputs: inputs,
            Outputs: outputs,
            DefinitionId: defId);
    }

    private static NodeData CreateGetVariableNode(string nodeId, string variableId)
    {
        var defId = GraphVariable.GetDefinitionPrefix + variableId;
        var outputs = new[]
        {
            new SocketData("Value", typeof(object).FullName!, IsInput: false, IsExecution: false)
        };

        return new NodeData(
            Id: nodeId,
            Name: "Get Variable",
            Callable: false,
            ExecInit: false,
            Inputs: Array.Empty<SocketData>(),
            Outputs: outputs,
            DefinitionId: defId);
    }
}

// â”€â”€ Shared helpers â”€â”€

internal static class TestConnections
{
    public static ConnectionData Exec(string outputNode, string outputSocket, string inputNode, string inputSocket)
        => new(outputNode, inputNode, outputSocket, inputSocket, true);

    public static ConnectionData Data(string outputNode, string outputSocket, string inputNode, string inputSocket)
        => new(outputNode, inputNode, outputSocket, inputSocket, false);
}

internal sealed class MinimalServiceProvider : IServiceProvider
{
    public object? GetService(Type serviceType) => null;
}

internal static partial class ExecutionTestHelpers
{
    public static async Task<bool> WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate()) return true;
            await Task.Delay(20);
        }
        return predicate();
    }
}
