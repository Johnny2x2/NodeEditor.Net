using System.Collections.Concurrent;
using System.Diagnostics;
using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Blazor.Tests;

public sealed class StreamingExecutionTests
{
    private static NodeExecutionService CreateService(out NodeRegistryService registry)
    {
        registry = new NodeRegistryService(new NodeDiscoveryService());
        registry.EnsureInitialized();
        RegisterInlineTestNodes(registry);
        return new NodeExecutionService(new ExecutionPlanner(), registry, new MinimalServiceProvider());
    }

    private static void RegisterInlineTestNodes(NodeRegistryService registry)
    {
        // Delay node: consumes execution time for timing assertions.
        var delayDef = NodeBuilder.Create("Test Delay")
            .Category("Test")
            .Callable()
            .Input<int>("DelayMs", 50)
            .OnExecute(async (ctx, ct) =>
            {
                await Task.Delay(ctx.GetInput<int>("DelayMs"), ct);
                await ctx.TriggerAsync("Exit");
            })
            .Build();

        // Capture a data input into a runtime variable.
        var captureDef = NodeBuilder.Create("Capture Value")
            .Category("Test")
            .Callable()
            .Input<string>("Key", "capture")
            .Input<object>("Value")
            .OnExecute(async (ctx, ct) =>
            {
                ctx.SetVariable(ctx.GetInput<string>("Key"), ctx.GetInput<object>("Value"));
                await ctx.TriggerAsync("Exit");
            })
            .Build();

        // Capture the count of a queue stored in variables.
        var captureQueueCountDef = NodeBuilder.Create("Capture Queue Count")
            .Category("Test")
            .Callable()
            .Input<string>("QueueKey", "items")
            .Input<string>("OutKey", "count")
            .OnExecute(async (ctx, ct) =>
            {
                var queueKey = ctx.GetInput<string>("QueueKey");
                var outKey = ctx.GetInput<string>("OutKey");
                var queue = ctx.GetVariable(queueKey) as ConcurrentQueue<object?>;
                ctx.SetVariable(outKey, queue?.Count ?? 0);
                await ctx.TriggerAsync("Exit");
            })
            .Build();

        // Thrower: used to verify sequential error propagation.
        var throwerDef = NodeBuilder.Create("Thrower")
            .Category("Test")
            .Callable()
            .OnExecute((ctx, ct) => throw new InvalidOperationException("Downstream error"))
            .Build();

        registry.RegisterDefinitions(new[] { delayDef, captureDef, captureQueueCountDef, throwerDef });
    }

    [Fact]
    public async Task EmitAsync_Sequential_WaitsForDownstream()
    {
        var service = CreateService(out var registry);

        var (nodes, connections) = new TestGraphBuilder()
            .AddNodeFromDefinition(registry, "Start", "start")
            .AddNodeFromDefinition(registry, "Test Stream", "stream", ("ItemCount", 5), ("DelayPerItemMs", 0))
            .AddNodeFromDefinition(registry, "Test Delay", "delay", ("DelayMs", 80))
            .ConnectExecution("start", "Exit", "stream", "Enter")
            .ConnectExecution("stream", "OnItem", "delay", "Enter")
            .Build();

        var context = new NodeRuntimeStorage();
        var options = NodeExecutionOptions.Default with { StreamMode = StreamMode.Sequential };

        var sw = Stopwatch.StartNew();
        await service.ExecuteAsync(nodes, connections, context, null!, options, CancellationToken.None);
        sw.Stop();

        // 5 items * 80ms = 400ms, allow some scheduler jitter.
        Assert.True(sw.ElapsedMilliseconds >= 320, $"Expected sequential downstream blocking; elapsed={sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task EmitAsync_Sequential_ItemsReceivedInOrder()
    {
        var service = CreateService(out var registry);

        var (nodes, connections) = new TestGraphBuilder()
            .AddNodeFromDefinition(registry, "Start", "start")
            .AddNodeFromDefinition(registry, "Test Stream", "stream", ("ItemCount", 5), ("DelayPerItemMs", 0))
            .AddNodeFromDefinition(registry, "Collector", "collector", ("Key", "items"))
            .ConnectExecution("start", "Exit", "stream", "Enter")
            .ConnectExecution("stream", "OnItem", "collector", "Enter")
            .ConnectData("stream", "Item", "collector", "Value")
            .Build();

        var context = new NodeRuntimeStorage();
        context.SetVariable("items", new ConcurrentQueue<object?>());

        var options = NodeExecutionOptions.Default with { StreamMode = StreamMode.Sequential };
        await service.ExecuteAsync(nodes, connections, context, null!, options, CancellationToken.None);

        var queue = Assert.IsType<ConcurrentQueue<object?>>(context.GetVariable("items"));
        var items = queue.ToArray().Cast<string>().ToArray();
        Assert.Equal(new[] { "item-0", "item-1", "item-2", "item-3", "item-4" }, items);
    }

    [Fact]
    public async Task EmitAsync_FireAndForget_DoesNotWaitForDownstream()
    {
        var service = CreateService(out var registry);

        var (nodes, connections) = new TestGraphBuilder()
            .AddNodeFromDefinition(registry, "Start", "start")
            .AddNodeFromDefinition(registry, "Test Stream", "stream", ("ItemCount", 5), ("DelayPerItemMs", 0))
            .AddNodeFromDefinition(registry, "Test Delay", "delay", ("DelayMs", 120))
            .ConnectExecution("start", "Exit", "stream", "Enter")
            .ConnectExecution("stream", "OnItem", "delay", "Enter")
            .Build();

        var context = new NodeRuntimeStorage();
        var options = NodeExecutionOptions.Default with { StreamMode = StreamMode.FireAndForget };

        var sw = Stopwatch.StartNew();
        await service.ExecuteAsync(nodes, connections, context, null!, options, CancellationToken.None);
        sw.Stop();

        // 5 items * 120ms = 600ms if sequential; fire-and-forget should be substantially faster.
        Assert.True(sw.ElapsedMilliseconds < 350, $"Expected fire-and-forget to not block; elapsed={sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task CompletedPath_FiresAfterAllItems()
    {
        var service = CreateService(out var registry);

        var (nodes, connections) = new TestGraphBuilder()
            .AddNodeFromDefinition(registry, "Start", "start")
            .AddNodeFromDefinition(registry, "Test Stream", "stream", ("ItemCount", 5), ("DelayPerItemMs", 0))
            .AddNodeFromDefinition(registry, "Collector", "collector", ("Key", "items"))
            .AddNodeFromDefinition(registry, "Test Delay", "delay", ("DelayMs", 60))
            .AddNodeFromDefinition(registry, "Capture Queue Count", "captureCount", ("QueueKey", "items"), ("OutKey", "completedCount"))
            .ConnectExecution("start", "Exit", "stream", "Enter")
            .ConnectExecution("stream", "OnItem", "collector", "Enter")
            .ConnectData("stream", "Item", "collector", "Value")
            .ConnectExecution("stream", "OnItem", "delay", "Enter")
            .ConnectExecution("stream", "Completed", "captureCount", "Enter")
            .Build();

        var context = new NodeRuntimeStorage();
        context.SetVariable("items", new ConcurrentQueue<object?>());

        var options = NodeExecutionOptions.Default with { StreamMode = StreamMode.FireAndForget };
        await service.ExecuteAsync(nodes, connections, context, null!, options, CancellationToken.None);

        Assert.Equal(5, context.GetVariable("completedCount"));
    }

    [Fact]
    public async Task CompletedPath_DownstreamHasAccessToFinalState()
    {
        var service = CreateService(out var registry);

        var (nodes, connections) = new TestGraphBuilder()
            .AddNodeFromDefinition(registry, "Start", "start")
            .AddNodeFromDefinition(registry, "Test Stream", "stream", ("ItemCount", 5), ("DelayPerItemMs", 0))
            .AddNodeFromDefinition(registry, "Capture Value", "captureFinal", ("Key", "finalItem"))
            .ConnectExecution("start", "Exit", "stream", "Enter")
            .ConnectExecution("stream", "Completed", "captureFinal", "Enter")
            .ConnectData("stream", "Item", "captureFinal", "Value")
            .Build();

        var context = new NodeRuntimeStorage();
        var options = NodeExecutionOptions.Default with { StreamMode = StreamMode.FireAndForget };

        await service.ExecuteAsync(nodes, connections, context, null!, options, CancellationToken.None);

        Assert.Equal("item-4", context.GetVariable("finalItem"));
    }

    [Fact]
    public async Task EmitAsync_DownstreamError_PropagatesInSequential()
    {
        var service = CreateService(out var registry);

        var (nodes, connections) = new TestGraphBuilder()
            .AddNodeFromDefinition(registry, "Start", "start")
            .AddNodeFromDefinition(registry, "Test Stream", "stream", ("ItemCount", 2), ("DelayPerItemMs", 0))
            .AddNodeFromDefinition(registry, "Thrower", "thrower")
            .ConnectExecution("start", "Exit", "stream", "Enter")
            .ConnectExecution("stream", "OnItem", "thrower", "Enter")
            .Build();

        var context = new NodeRuntimeStorage();
        var options = NodeExecutionOptions.Default with { StreamMode = StreamMode.Sequential };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExecuteAsync(nodes, connections, context, null!, options, CancellationToken.None));
    }

    [Fact]
    public async Task EmitAsync_Cancellation_StopsStreaming()
    {
        var service = CreateService(out var registry);

        var (nodes, connections) = new TestGraphBuilder()
            .AddNodeFromDefinition(registry, "Start", "start")
            .AddNodeFromDefinition(registry, "Test Stream", "stream", ("ItemCount", 100), ("DelayPerItemMs", 25))
            .AddNodeFromDefinition(registry, "Collector", "collector", ("Key", "items"))
            .ConnectExecution("start", "Exit", "stream", "Enter")
            .ConnectExecution("stream", "OnItem", "collector", "Enter")
            .ConnectData("stream", "Item", "collector", "Value")
            .Build();

        var context = new NodeRuntimeStorage();
        context.SetVariable("items", new ConcurrentQueue<object?>());

        var cts = new CancellationTokenSource();
        cts.CancelAfter(80);

        var options = NodeExecutionOptions.Default with { StreamMode = StreamMode.Sequential };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ExecuteAsync(nodes, connections, context, null!, options, cts.Token));

        var queue = Assert.IsType<ConcurrentQueue<object?>>(context.GetVariable("items"));
        Assert.True(queue.Count < 100, "Cancellation should interrupt the streaming loop");
    }
}
