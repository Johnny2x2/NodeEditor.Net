using System.Collections.Concurrent;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services;
using NodeEditor.Blazor.Services.Execution;

namespace NodeEditor.Blazor.Tests;

public sealed class ExecutionEngineTests
{
    [Fact]
    public async Task SequentialExecution_ResolvesDataDependencies()
    {
        var nodes = new List<NodeData>
        {
            TestNodes.Start("start"),
            TestNodes.Const("const", value: 7),
            TestNodes.Add("add")
        };

        var connections = new List<ConnectionData>
        {
            TestConnections.Exec("start", "Exit", "add", "Enter"),
            TestConnections.Data("const", "Value", "add", "Value")
        };

        var context = new NodeExecutionContext();
        var service = TestNodes.CreateExecutor();
        var testContext = new TestNodeContext { ConstValue = 7 };

        await service.ExecuteAsync(nodes, connections, context, testContext, NodeExecutionOptions.Default, CancellationToken.None);

        Assert.True(context.IsNodeExecuted("const"));
        Assert.True(context.IsNodeExecuted("add"));
        Assert.Equal(10, context.GetSocketValue("add", "Result"));
    }

    [Fact]
    public async Task SequentialExecution_FollowsSignaledBranch()
    {
        var nodes = new List<NodeData>
        {
            TestNodes.Start("start"),
            TestNodes.Branch("branch", condValue: true),
            TestNodes.Marker("trueNode"),
            TestNodes.Marker("falseNode")
        };

        var connections = new List<ConnectionData>
        {
            TestConnections.Exec("start", "Exit", "branch", "Enter"),
            TestConnections.Exec("branch", "True", "trueNode", "Enter"),
            TestConnections.Exec("branch", "False", "falseNode", "Enter")
        };

        var context = new NodeExecutionContext();
        var service = TestNodes.CreateExecutor();

        await service.ExecuteAsync(nodes, connections, context, new TestNodeContext(), NodeExecutionOptions.Default, CancellationToken.None);

        Assert.True(context.IsNodeExecuted("trueNode"));
        Assert.False(context.IsNodeExecuted("falseNode"));
    }

    [Fact]
    public async Task ParallelExecution_RunsIndependentNodesConcurrently()
    {
        var nodes = new List<NodeData>
        {
            TestNodes.Delay("start1", isEntry: true, delayMs: 120),
            TestNodes.Delay("start2", isEntry: true, delayMs: 120)
        };

        var connections = new List<ConnectionData>();
        var context = new NodeExecutionContext();
        var testContext = new TestNodeContext();
        var service = TestNodes.CreateExecutor();

        var options = new NodeExecutionOptions(ExecutionMode.Parallel, AllowBackground: false, MaxDegreeOfParallelism: 4);

        await service.ExecuteAsync(nodes, connections, context, testContext, options, CancellationToken.None);

        Assert.True(testContext.MaxConcurrent >= 2);
    }

    [Fact]
    public async Task ParallelExecution_RespectsDependencyLayers()
    {
        var nodes = new List<NodeData>
        {
            TestNodes.DelayedValue("a", delayMs: 150),
            TestNodes.DelayedValue("b", delayMs: 150),
            TestNodes.Sum("c")
        };

        var connections = new List<ConnectionData>
        {
            TestConnections.Data("a", "Value", "c", "A"),
            TestConnections.Data("b", "Value", "c", "B")
        };

        var context = new NodeExecutionContext();
        var testContext = new TestNodeContext();
        var service = TestNodes.CreateExecutor();

        var started = new ConcurrentDictionary<string, DateTime>();
        var completed = new ConcurrentDictionary<string, DateTime>();

        service.NodeStarted += (_, args) => started[args.Node.Id] = DateTime.UtcNow;
        service.NodeCompleted += (_, args) => completed[args.Node.Id] = DateTime.UtcNow;

        var options = new NodeExecutionOptions(ExecutionMode.Parallel, AllowBackground: false, MaxDegreeOfParallelism: 4);

        await service.ExecuteAsync(nodes, connections, context, testContext, options, CancellationToken.None);

        Assert.True(started.ContainsKey("c"));
        Assert.True(completed.ContainsKey("a"));
        Assert.True(completed.ContainsKey("b"));

        Assert.True(started["c"] >= completed["a"]);
        Assert.True(started["c"] >= completed["b"]);
    }

    [Fact]
    public async Task Cancellation_StopsExecutionWithinTimeout()
    {
        var nodes = new List<NodeData>
        {
            TestNodes.Delay("start", isEntry: true, delayMs: 2000)
        };

        var connections = new List<ConnectionData>();
        var context = new NodeExecutionContext();
        var testContext = new TestNodeContext();
        var service = TestNodes.CreateExecutor();

        using var cts = new CancellationTokenSource(100);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ExecuteAsync(nodes, connections, context, testContext, NodeExecutionOptions.Default, cts.Token));
    }

    [Fact]
    public async Task BackgroundQueue_ExecutesPlannedJobs()
    {
        var nodes = new List<NodeData>
        {
            TestNodes.Start("start"),
            TestNodes.Marker("marker")
        };

        var connections = new List<ConnectionData>
        {
            TestConnections.Exec("start", "Exit", "marker", "Enter")
        };

        var context = new NodeExecutionContext();
        var testContext = new TestNodeContext();
        var planner = new ExecutionPlanner();
        var service = new NodeExecutionService(planner, new SocketTypeResolver());
        var queue = new BackgroundExecutionQueue();
        var worker = new BackgroundExecutionWorker(queue, service);

        var plan = planner.BuildPlan(nodes, connections);
        var job = new ExecutionJob(Guid.NewGuid(), plan, connections, context, testContext, NodeExecutionOptions.Default);
        await queue.EnqueueAsync(job);

        using var cts = new CancellationTokenSource();
        var runTask = worker.RunAsync(cts.Token);

        var executed = await ExecutionTestHelpers.WaitUntilAsync(() => context.IsNodeExecuted("marker"), TimeSpan.FromSeconds(2));
        cts.Cancel();

        try
        {
            await runTask;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.True(executed);
    }

    [Fact]
    public async Task GroupExecution_ReturnsOutputsToParentContext()
    {
        var groupNodes = new List<NodeData>
        {
            TestNodes.Const("innerConst", value: 42)
        };

        var group = new GroupNodeData(
            Id: "group",
            Name: "Group",
            Nodes: groupNodes,
            Connections: Array.Empty<ConnectionData>(),
            Inputs: Array.Empty<SocketData>(),
            Outputs: new[] { new SocketData("Value", typeof(int).FullName ?? "System.Int32", false, false) },
            InputMappings: Array.Empty<GroupSocketMapping>(),
            OutputMappings: new[] { new GroupSocketMapping("Value", "innerConst", "Value") });

        var parentContext = new NodeExecutionContext();
        var service = TestNodes.CreateExecutor();

        await service.ExecuteGroupAsync(group, parentContext, new TestNodeContext(), NodeExecutionOptions.Default, CancellationToken.None);

        Assert.Equal(42, parentContext.GetSocketValue("group", "Value"));
    }
}

internal static class TestNodes
{
    public static NodeExecutionService CreateExecutor()
        => new(new ExecutionPlanner(), new SocketTypeResolver());

    public static NodeData Start(string id)
        => new(id, "Start", true, true,
            Inputs: Array.Empty<SocketData>(),
            Outputs: new[] { ExecOutput("Exit") });

    public static NodeData Const(string id, int value)
        => new(id, "Const", false, false,
            Inputs: Array.Empty<SocketData>(),
            Outputs: new[] { DataOutput("Value", typeof(int).FullName ?? "System.Int32", SocketValue.FromObject(value)) });

    public static NodeData Add(string id)
        => new(id, "Add", true, false,
            Inputs: new[] { ExecInput("Enter"), DataInput("Value", typeof(int).FullName ?? "System.Int32") },
            Outputs: new[] { DataOutput("Result", typeof(int).FullName ?? "System.Int32"), ExecOutput("Exit") });

    public static NodeData Branch(string id, bool condValue)
        => new(id, "Branch", true, false,
            Inputs: new[] { ExecInput("Enter"), DataInput("cond", typeof(bool).FullName ?? "System.Boolean", SocketValue.FromObject(condValue)) },
            Outputs: new[] { ExecOutput("True"), ExecOutput("False") });

    public static NodeData Marker(string id)
        => new(id, "Marker", true, false,
            Inputs: new[] { ExecInput("Enter") },
            Outputs: new[] { DataOutput("Hit", typeof(bool).FullName ?? "System.Boolean"), ExecOutput("Exit") });

    public static NodeData Delay(string id, bool isEntry, int delayMs)
        => new(id, "Delay", true, isEntry,
            Inputs: new[]
            {
                ExecInput("Enter"),
                DataInput("DelayMs", typeof(int).FullName ?? "System.Int32", SocketValue.FromObject(delayMs))
            },
            Outputs: new[] { ExecOutput("Exit") });

    public static NodeData DelayedValue(string id, int delayMs)
        => new(id, "DelayedValue", true, false,
            Inputs: new[] { DataInput("DelayMs", typeof(int).FullName ?? "System.Int32", SocketValue.FromObject(delayMs)) },
            Outputs: new[] { DataOutput("Value", typeof(int).FullName ?? "System.Int32") });

    public static NodeData Sum(string id)
        => new(id, "Sum", true, false,
            Inputs: new[]
            {
                DataInput("A", typeof(int).FullName ?? "System.Int32"),
                DataInput("B", typeof(int).FullName ?? "System.Int32")
            },
            Outputs: new[] { DataOutput("Result", typeof(int).FullName ?? "System.Int32") });

    private static SocketData ExecInput(string name)
        => new(name, typeof(ExecutionPath).FullName ?? nameof(ExecutionPath), true, true);

    private static SocketData ExecOutput(string name)
        => new(name, typeof(ExecutionPath).FullName ?? nameof(ExecutionPath), false, true);

    private static SocketData DataInput(string name, string typeName, SocketValue? value = null)
        => new(name, typeName, true, false, value);

    private static SocketData DataOutput(string name, string typeName, SocketValue? value = null)
        => new(name, typeName, false, false, value);
}

internal static class TestConnections
{
    public static ConnectionData Exec(string outputNode, string outputSocket, string inputNode, string inputSocket)
        => new(outputNode, inputNode, outputSocket, inputSocket, true);

    public static ConnectionData Data(string outputNode, string outputSocket, string inputNode, string inputSocket)
        => new(outputNode, inputNode, outputSocket, inputSocket, false);
}

internal sealed class TestNodeContext : INodeMethodContext
{
    private int _current;
    private int _max;

    public NodeData? CurrentProcessingNode { get; set; }

#pragma warning disable CS0067 // Event is never used
    public event Action<string, NodeData, ExecutionFeedbackType, object?, bool>? FeedbackInfo;
#pragma warning restore CS0067

    public int MaxConcurrent => _max;

    public int ConstValue { get; set; } = 42;

    [Node("Start")]
    public void Start(out ExecutionPath Exit)
    {
        Exit = new ExecutionPath();
        Exit.Signal();
    }

    [Node("Const")]
    public void Const(out int Value)
    {
        Value = ConstValue;
    }

    [Node("Add")]
    public void Add(ExecutionPath Enter, int Value, out int Result, out ExecutionPath Exit)
    {
        Result = Value + 3;
        Exit = new ExecutionPath();
        Exit.Signal();
    }

    [Node("Branch")]
    public void Branch(ExecutionPath Enter, bool cond, out ExecutionPath True, out ExecutionPath False)
    {
        True = new ExecutionPath();
        False = new ExecutionPath();

        if (cond)
        {
            True.Signal();
        }
        else
        {
            False.Signal();
        }
    }

    [Node("Marker")]
    public void Marker(ExecutionPath Enter, out bool Hit, out ExecutionPath Exit)
    {
        Hit = true;
        Exit = new ExecutionPath();
        Exit.Signal();
    }

    [Node("Delay")]
    public Task Delay(ExecutionPath Enter, int DelayMs, CancellationToken token, out ExecutionPath Exit)
    {
        Exit = new ExecutionPath();
        Exit.Signal();

        var current = Interlocked.Increment(ref _current);
        UpdateMax(current);

        return Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DelayMs, token);
            }
            finally
            {
                Interlocked.Decrement(ref _current);
            }
        }, token);
    }

    [Node("DelayedValue")]
    public Task DelayedValue(int DelayMs, CancellationToken token, out int Value)
    {
        Value = DelayMs;
        return Task.Delay(DelayMs, token);
    }

    [Node("Sum")]
    public void Sum(int A, int B, out int Result)
    {
        Result = A + B;
    }

    private void UpdateMax(int current)
    {
        int initial;
        do
        {
            initial = _max;
            if (current <= initial)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref _max, current, initial) != initial);
    }
}

internal static partial class ExecutionTestHelpers
{
    public static async Task<bool> WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
            {
                return true;
            }

            await Task.Delay(20);
        }

        return predicate();
    }
}
