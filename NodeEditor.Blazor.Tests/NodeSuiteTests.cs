using NodeEditor.Net.Models;
using NodeEditor.Net.Services;
using NodeEditor.Net.Services.Execution;

namespace NodeEditor.Blazor.Tests;

public sealed class NodeSuiteTests
{
    [Fact]
    public async Task StringNodes_ConcatAndLength_PipelineWorks()
    {
        var nodes = new List<NodeData>
        {
            TestNodeFactory.Start("start"),
            TestNodeFactory.StringConcat("concat", "Hello ", "World"),
            TestNodeFactory.StringLength("length"),
            TestNodeFactory.SinkInt("sink")
        };

        var connections = new List<ConnectionData>
        {
            TestConnections.Exec("start", "Exit", "sink", "Enter"),
            TestConnections.Data("concat", "Result", "length", "Input"),
            TestConnections.Data("length", "Length", "sink", "Value")
        };

        var context = new NodeExecutionContext();
        var service = TestNodeFactory.CreateExecutor();
        var testContext = new NodeSuiteTestContext();

        await service.ExecuteAsync(nodes, connections, context, testContext, NodeExecutionOptions.Default, CancellationToken.None);

        Assert.Equal(11, context.GetSocketValue("sink", "Observed"));
    }

    [Fact]
    public async Task NumberNodes_Clamp_Works()
    {
        var nodes = new List<NodeData>
        {
            TestNodeFactory.Start("start"),
            TestNodeFactory.Clamp("clamp", value: 42, min: 0, max: 10),
            TestNodeFactory.SinkInt("sink")
        };

        var connections = new List<ConnectionData>
        {
            TestConnections.Exec("start", "Exit", "sink", "Enter"),
            TestConnections.Data("clamp", "Result", "sink", "Value")
        };

        var context = new NodeExecutionContext();
        var service = TestNodeFactory.CreateExecutor();
        var testContext = new NodeSuiteTestContext();

        await service.ExecuteAsync(nodes, connections, context, testContext, NodeExecutionOptions.Default, CancellationToken.None);

        Assert.Equal(10, context.GetSocketValue("sink", "Observed"));
    }

    [Fact]
    public async Task ListNodes_Slice_Works()
    {
        var nodes = new List<NodeData>
        {
            TestNodeFactory.Start("start"),
            TestNodeFactory.ListCreate("list", "A", "B", "C", "D"),
            TestNodeFactory.ListSlice("slice", start: 1, count: 2),
            TestNodeFactory.ListCount("count"),
            TestNodeFactory.SinkInt("sink")
        };

        var connections = new List<ConnectionData>
        {
            TestConnections.Exec("start", "Exit", "sink", "Enter"),
            TestConnections.Data("list", "List", "slice", "List"),
            TestConnections.Data("slice", "Result", "count", "List"),
            TestConnections.Data("count", "Count", "sink", "Value")
        };

        var context = new NodeExecutionContext();
        var service = TestNodeFactory.CreateExecutor();
        var testContext = new NodeSuiteTestContext();

        await service.ExecuteAsync(nodes, connections, context, testContext, NodeExecutionOptions.Default, CancellationToken.None);

        Assert.Equal(2, context.GetSocketValue("sink", "Observed"));
    }

    [Fact]
    public async Task LoopNodes_ForLoopStep_IteratesAndExits()
    {
        var nodes = new List<NodeData>
        {
            TestNodeFactory.Start("start"),
            TestNodeFactory.ForLoopStep("loop", start: 0, end: 2, step: 1),
            TestNodeFactory.Marker("end")
        };

        var connections = new List<ConnectionData>
        {
            TestConnections.Exec("start", "Exit", "loop", "Enter"),
            TestConnections.Exec("loop", "Exit", "end", "Enter")
        };

        var context = new NodeExecutionContext();
        var service = TestNodeFactory.CreateExecutor();
        var testContext = new NodeSuiteTestContext();

        await service.ExecuteAsync(nodes, connections, context, testContext, NodeExecutionOptions.Default, CancellationToken.None);

        Assert.Equal(4, testContext.ForLoopCalls);
        Assert.True(context.IsNodeExecuted("end"));
    }
}

internal static class TestNodeFactory
{
    public static NodeExecutionService CreateExecutor()
        => new(new ExecutionPlanner(), new SocketTypeResolver());

    public static NodeData Start(string id)
        => new(id, "Start", true, true,
            Inputs: Array.Empty<SocketData>(),
            Outputs: new[] { ExecOutput("Exit") });

    public static NodeData StringConcat(string id, string a, string b)
        => new(id, "String Concat", false, false,
            Inputs: new[]
            {
                DataInput("A", typeof(string).FullName ?? "System.String", SocketValue.FromObject(a)),
                DataInput("B", typeof(string).FullName ?? "System.String", SocketValue.FromObject(b))
            },
            Outputs: new[] { DataOutput("Result", typeof(string).FullName ?? "System.String") });

    public static NodeData StringLength(string id)
        => new(id, "String Length", false, false,
            Inputs: new[] { DataInput("Input", typeof(string).FullName ?? "System.String") },
            Outputs: new[] { DataOutput("Length", typeof(int).FullName ?? "System.Int32") });

    public static NodeData Clamp(string id, int value, int min, int max)
        => new(id, "Clamp", false, false,
            Inputs: new[]
            {
                DataInput("Value", typeof(int).FullName ?? "System.Int32", SocketValue.FromObject(value)),
                DataInput("Min", typeof(int).FullName ?? "System.Int32", SocketValue.FromObject(min)),
                DataInput("Max", typeof(int).FullName ?? "System.Int32", SocketValue.FromObject(max))
            },
            Outputs: new[] { DataOutput("Result", typeof(int).FullName ?? "System.Int32") });

    public static NodeData ListCreate(string id, string a, string b, string c, string d)
        => new(id, "List Create", false, false,
            Inputs: new[]
            {
                DataInput("A", typeof(string).FullName ?? "System.String", SocketValue.FromObject(a)),
                DataInput("B", typeof(string).FullName ?? "System.String", SocketValue.FromObject(b)),
                DataInput("C", typeof(string).FullName ?? "System.String", SocketValue.FromObject(c)),
                DataInput("D", typeof(string).FullName ?? "System.String", SocketValue.FromObject(d))
            },
            Outputs: new[] { DataOutput("List", typeof(List<string>).FullName ?? "System.Collections.Generic.List`1") });

    public static NodeData ListSlice(string id, int start, int count)
        => new(id, "List Slice", false, false,
            Inputs: new[]
            {
                DataInput("List", typeof(List<string>).FullName ?? "System.Collections.Generic.List`1"),
                DataInput("Start", typeof(int).FullName ?? "System.Int32", SocketValue.FromObject(start)),
                DataInput("Count", typeof(int).FullName ?? "System.Int32", SocketValue.FromObject(count))
            },
            Outputs: new[] { DataOutput("Result", typeof(List<string>).FullName ?? "System.Collections.Generic.List`1") });

    public static NodeData ListCount(string id)
        => new(id, "List Count", false, false,
            Inputs: new[] { DataInput("List", typeof(List<string>).FullName ?? "System.Collections.Generic.List`1") },
            Outputs: new[] { DataOutput("Count", typeof(int).FullName ?? "System.Int32") });

    public static NodeData ForLoopStep(string id, int start, int end, int step)
        => new(id, "For Loop Step", true, false,
            Inputs: new[]
            {
                ExecInput("Enter"),
                DataInput("Start", typeof(int).FullName ?? "System.Int32", SocketValue.FromObject(start)),
                DataInput("End", typeof(int).FullName ?? "System.Int32", SocketValue.FromObject(end)),
                DataInput("Step", typeof(int).FullName ?? "System.Int32", SocketValue.FromObject(step))
            },
            Outputs: new[] { ExecOutput("Exit"), ExecOutput("LoopPath"), DataOutput("Index", typeof(int).FullName ?? "System.Int32") });

    public static NodeData SinkInt(string id)
        => new(id, "Sink", true, false,
            Inputs: new[] { ExecInput("Enter"), DataInput("Value", typeof(int).FullName ?? "System.Int32") },
            Outputs: new[] { DataOutput("Observed", typeof(int).FullName ?? "System.Int32"), ExecOutput("Exit") });

    public static NodeData Marker(string id)
        => new(id, "Marker", true, false,
            Inputs: new[] { ExecInput("Enter") },
            Outputs: new[] { ExecOutput("Exit") });

    private static SocketData ExecInput(string name)
        => new(name, typeof(ExecutionPath).FullName ?? nameof(ExecutionPath), true, true);

    private static SocketData ExecOutput(string name)
        => new(name, typeof(ExecutionPath).FullName ?? nameof(ExecutionPath), false, true);

    private static SocketData DataInput(string name, string typeName, SocketValue? value = null)
        => new(name, typeName, true, false, value);

    private static SocketData DataOutput(string name, string typeName, SocketValue? value = null)
        => new(name, typeName, false, false, value);
}

internal sealed class NodeSuiteTestContext : INodeMethodContext
{
    private readonly Dictionary<string, double> _loopState = new(StringComparer.Ordinal);

    public NodeData? CurrentProcessingNode { get; set; }

#pragma warning disable CS0067 // Event is never used
    public event Action<string, NodeData, ExecutionFeedbackType, object?, bool>? FeedbackInfo;
#pragma warning restore CS0067

    public int ForLoopCalls { get; private set; }

    [Node("Start", isCallable: true, isExecutionInitiator: true)]
    public void Start(out ExecutionPath Exit)
    {
        Exit = new ExecutionPath();
        Exit.Signal();
    }

    [Node("String Concat", isCallable: false)]
    public void StringConcat(string A, string B, out string Result)
    {
        Result = string.Concat(A ?? string.Empty, B ?? string.Empty);
    }

    [Node("String Length", isCallable: false)]
    public void StringLength(string Input, out int Length)
    {
        Length = (Input ?? string.Empty).Length;
    }

    [Node("Clamp", isCallable: false)]
    public void Clamp(int Value, int Min, int Max, out int Result)
    {
        var minVal = Math.Min(Min, Max);
        var maxVal = Math.Max(Min, Max);
        Result = Math.Min(Math.Max(Value, minVal), maxVal);
    }

    [Node("List Create", isCallable: false)]
    public void ListCreate(string A, string B, string C, string D, out List<string> List)
    {
        List = new List<string> { A ?? string.Empty, B ?? string.Empty, C ?? string.Empty, D ?? string.Empty };
    }

    [Node("List Slice", isCallable: false)]
    public void ListSlice(List<string> List, int Start, int Count, out List<string> Result)
    {
        var startIndex = Math.Max(0, Start);
        var length = Math.Max(0, Count);

        if (startIndex >= List.Count)
        {
            Result = new List<string>();
            return;
        }

        var end = Math.Min(List.Count, startIndex + length);
        Result = List.GetRange(startIndex, end - startIndex);
    }

    [Node("List Count", isCallable: false)]
    public void ListCount(List<string> List, out int Count)
    {
        Count = List.Count;
    }

    [Node("For Loop Step", isCallable: true)]
    public void ForLoopStep(int Start, int End, int Step, out ExecutionPath Exit, out ExecutionPath LoopPath, out int Index)
    {
        ForLoopCalls++;
        Exit = new ExecutionPath();
        LoopPath = new ExecutionPath();

        if (Step == 0)
        {
            Index = Start;
            Exit.Signal();
            return;
        }

        var key = CurrentProcessingNode?.Id ?? "loop";
        if (!_loopState.TryGetValue(key, out var current))
        {
            current = Start;
        }

        var shouldExit = Step > 0 ? current > End : current < End;
        if (shouldExit)
        {
            Index = (int)(current - Step);
            _loopState.Remove(key);
            Exit.Signal();
            return;
        }

        Index = (int)current;
        current += Step;
        _loopState[key] = current;
        LoopPath.Signal();
    }

    [Node("Sink", isCallable: true)]
    public void Sink(ExecutionPath Enter, int Value, out int Observed, out ExecutionPath Exit)
    {
        Observed = Value;
        Exit = new ExecutionPath();
        Exit.Signal();
    }

    [Node("Marker", isCallable: true)]
    public void Marker(ExecutionPath Enter, out ExecutionPath Exit)
    {
        Exit = new ExecutionPath();
        Exit.Signal();
    }
}
