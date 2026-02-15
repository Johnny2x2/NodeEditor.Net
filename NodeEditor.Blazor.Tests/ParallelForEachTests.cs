using System.Collections.Concurrent;
using System.Diagnostics;
using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Blazor.Tests;

public sealed class ParallelForEachTests
{
    private static NodeExecutionService CreateService(out NodeRegistryService registry)
    {
        registry = new NodeRegistryService(new NodeDiscoveryService());
        registry.EnsureInitialized();
        return new NodeExecutionService(new ExecutionPlanner(), registry, new MinimalServiceProvider());
    }

    private static void RegisterTestHelperNodes(NodeRegistryService registry)
    {
        // Thread-safe collector that records items into a ConcurrentBag variable.
        var collectorDef = NodeBuilder.Create("Parallel Collector")
            .Category("Test").Callable()
            .Input<object>("Value")
            .Input<string>("Key", "collected")
            .OnExecute(async (ctx, ct) =>
            {
                var key = ctx.GetInput<string>("Key");
                var value = ctx.GetInput<object>("Value");

                // Use a ConcurrentBag stored in a graph variable for thread-safe collection.
                if (ctx.GetVariable(key) is not ConcurrentBag<object?> bag)
                {
                    bag = new ConcurrentBag<object?>();
                    ctx.SetVariable(key, bag);
                }

                bag.Add(value);
                await ctx.TriggerAsync("Exit");
            })
            .Build();

        // Delay node
        var delayDef = NodeBuilder.Create("Parallel Delay")
            .Category("Test").Callable()
            .Input<int>("DelayMs", 50)
            .OnExecute(async (ctx, ct) =>
            {
                await Task.Delay(ctx.GetInput<int>("DelayMs"), ct);
                await ctx.TriggerAsync("Exit");
            })
            .Build();

        // Concurrency tracker: records the max concurrency observed.
        // Uses ConcurrentDictionary for all shared state since parallel iterations
        // have isolated variable scopes — only in-place mutation of shared references works.
        var concurrencyDef = NodeBuilder.Create("Concurrency Tracker")
            .Category("Test").Callable()
            .Input<int>("TrackDelayMs", 100)
            .OnExecute(async (ctx, ct) =>
            {
                const string counterKey = "__concurrency_counter";

                var counter = ctx.GetVariable(counterKey) as ConcurrentDictionary<string, int>;
                if (counter is null) { await ctx.TriggerAsync("Exit"); return; }

                var current = counter.AddOrUpdate("active", 1, (_, v) => v + 1);

                // Atomically update max
                counter.AddOrUpdate("max", current, (_, existing) => Math.Max(existing, current));

                await Task.Delay(ctx.GetInput<int>("TrackDelayMs"), ct);

                counter.AddOrUpdate("active", 0, (_, v) => v - 1);
                await ctx.TriggerAsync("Exit");
            })
            .Build();

        registry.RegisterDefinitions(new[] { collectorDef, delayDef, concurrencyDef });
    }

    [Fact]
    public async Task IteratesAllItems()
    {
        var service = CreateService(out var registry);
        RegisterTestHelperNodes(registry);

        var list = new SerializableList();
        list.Add("A");
        list.Add("B");
        list.Add("C");

        var (nodes, connections) = new TestGraphBuilder()
            .AddNodeFromDefinition(registry, "Start", "start")
            .AddNodeFromDefinition(registry, "Parallel ForEach", "ploop",
                ("List", list), ("MaxParallelism", 4))
            .AddNodeFromDefinition(registry, "Parallel Collector", "collect",
                ("Key", "items"))
            .AddNodeFromDefinition(registry, "Marker", "end")
            .ConnectExecution("start", "Exit", "ploop", "Enter")
            .ConnectExecution("ploop", "LoopPath", "collect", "Enter")
            .ConnectData("ploop", "Item", "collect", "Value")
            .ConnectExecution("ploop", "Exit", "end", "Enter")
            .Build();

        var context = new NodeRuntimeStorage();
        // Pre-seed the shared collection variable so all scoped iterations
        // find it via read-through and mutate it in-place.
        context.SetVariable("items", new ConcurrentBag<object?>());
        await service.ExecuteAsync(nodes, connections, context, null!,
            NodeExecutionOptions.Default, CancellationToken.None);

        Assert.True(context.IsNodeExecuted("end"), "Exit should fire after all iterations");

        var collected = context.GetVariable("items") as ConcurrentBag<object?>;
        Assert.NotNull(collected);
        var items = collected!.ToHashSet();
        Assert.Contains("A", items);
        Assert.Contains("B", items);
        Assert.Contains("C", items);
        Assert.Equal(3, items.Count);
    }

    [Fact]
    public async Task EmptyList_TriggersExitImmediately()
    {
        var service = CreateService(out var registry);

        var list = new SerializableList();

        var (nodes, connections) = new TestGraphBuilder()
            .AddNodeFromDefinition(registry, "Start", "start")
            .AddNodeFromDefinition(registry, "Parallel ForEach", "ploop",
                ("List", list))
            .AddNodeFromDefinition(registry, "Marker", "end")
            .ConnectExecution("start", "Exit", "ploop", "Enter")
            .ConnectExecution("ploop", "Exit", "end", "Enter")
            .Build();

        var context = new NodeRuntimeStorage();
        await service.ExecuteAsync(nodes, connections, context, null!,
            NodeExecutionOptions.Default, CancellationToken.None);

        Assert.True(context.IsNodeExecuted("end"), "Exit should fire for empty list");
    }

    [Fact]
    public async Task NullList_TriggersExitImmediately()
    {
        var service = CreateService(out var registry);

        var (nodes, connections) = new TestGraphBuilder()
            .AddNodeFromDefinition(registry, "Start", "start")
            .AddNodeFromDefinition(registry, "Parallel ForEach", "ploop")
            .AddNodeFromDefinition(registry, "Marker", "end")
            .ConnectExecution("start", "Exit", "ploop", "Enter")
            .ConnectExecution("ploop", "Exit", "end", "Enter")
            .Build();

        var context = new NodeRuntimeStorage();
        await service.ExecuteAsync(nodes, connections, context, null!,
            NodeExecutionOptions.Default, CancellationToken.None);

        Assert.True(context.IsNodeExecuted("end"), "Exit should fire for null list");
    }

    [Fact]
    public async Task ExecutesInParallel_FasterThanSequential()
    {
        var service = CreateService(out var registry);
        RegisterTestHelperNodes(registry);

        var list = new SerializableList();
        for (int i = 0; i < 5; i++) list.Add(i);

        var (nodes, connections) = new TestGraphBuilder()
            .AddNodeFromDefinition(registry, "Start", "start")
            .AddNodeFromDefinition(registry, "Parallel ForEach", "ploop",
                ("List", list), ("MaxParallelism", 5))
            .AddNodeFromDefinition(registry, "Parallel Delay", "delay", ("DelayMs", 200))
            .AddNodeFromDefinition(registry, "Marker", "end")
            .ConnectExecution("start", "Exit", "ploop", "Enter")
            .ConnectExecution("ploop", "LoopPath", "delay", "Enter")
            .ConnectExecution("ploop", "Exit", "end", "Enter")
            .Build();

        var context = new NodeRuntimeStorage();
        var sw = Stopwatch.StartNew();
        await service.ExecuteAsync(nodes, connections, context, null!,
            NodeExecutionOptions.Default, CancellationToken.None);
        sw.Stop();

        Assert.True(context.IsNodeExecuted("end"));
        // Sequential would take ~1000ms (5 × 200ms). Parallel should be ~200ms.
        // Use generous threshold to avoid flaky tests on slow machines.
        Assert.True(sw.ElapsedMilliseconds < 800,
            $"Expected parallel execution < 800ms but took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task MaxParallelism_ThrottlesConcurrency()
    {
        var service = CreateService(out var registry);
        RegisterTestHelperNodes(registry);

        var list = new SerializableList();
        for (int i = 0; i < 8; i++) list.Add(i);

        var (nodes, connections) = new TestGraphBuilder()
            .AddNodeFromDefinition(registry, "Start", "start")
            .AddNodeFromDefinition(registry, "Parallel ForEach", "ploop",
                ("List", list), ("MaxParallelism", 2))
            .AddNodeFromDefinition(registry, "Concurrency Tracker", "tracker", ("TrackDelayMs", 100))
            .AddNodeFromDefinition(registry, "Marker", "end")
            .ConnectExecution("start", "Exit", "ploop", "Enter")
            .ConnectExecution("ploop", "LoopPath", "tracker", "Enter")
            .ConnectExecution("ploop", "Exit", "end", "Enter")
            .Build();

        var context = new NodeRuntimeStorage();
        // Pre-seed the shared concurrency tracking variable.
        context.SetVariable("__concurrency_counter", new ConcurrentDictionary<string, int>(StringComparer.Ordinal));
        await service.ExecuteAsync(nodes, connections, context, null!,
            NodeExecutionOptions.Default, CancellationToken.None);

        Assert.True(context.IsNodeExecuted("end"));

        var counter = context.GetVariable("__concurrency_counter") as ConcurrentDictionary<string, int>;
        Assert.NotNull(counter);
        var maxConcurrency = counter!.GetValueOrDefault("max", 0);
        Assert.True(maxConcurrency >= 1, "Should have recorded at least one concurrent execution");
        Assert.True(maxConcurrency <= 2,
            $"Expected max concurrency ≤ 2 but observed {maxConcurrency}");
    }

    [Fact]
    public async Task Cancellation_StopsIteration()
    {
        var service = CreateService(out var registry);
        RegisterTestHelperNodes(registry);

        var list = new SerializableList();
        for (int i = 0; i < 20; i++) list.Add(i);

        var (nodes, connections) = new TestGraphBuilder()
            .AddNodeFromDefinition(registry, "Start", "start")
            .AddNodeFromDefinition(registry, "Parallel ForEach", "ploop",
                ("List", list), ("MaxParallelism", 2))
            .AddNodeFromDefinition(registry, "Parallel Delay", "delay", ("DelayMs", 500))
            .AddNodeFromDefinition(registry, "Marker", "end")
            .ConnectExecution("start", "Exit", "ploop", "Enter")
            .ConnectExecution("ploop", "LoopPath", "delay", "Enter")
            .ConnectExecution("ploop", "Exit", "end", "Enter")
            .Build();

        using var cts = new CancellationTokenSource(200);
        var context = new NodeRuntimeStorage();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ExecuteAsync(nodes, connections, context, null!,
                NodeExecutionOptions.Default, cts.Token));
    }

    [Fact]
    public async Task IterationsAreIsolated_OutputsDoNotInterfere()
    {
        // Verify that each iteration sees its own Item value, not another iteration's.
        var service = CreateService(out var registry);
        RegisterTestHelperNodes(registry);

        var list = new SerializableList();
        for (int i = 0; i < 10; i++) list.Add($"item_{i}");

        var (nodes, connections) = new TestGraphBuilder()
            .AddNodeFromDefinition(registry, "Start", "start")
            .AddNodeFromDefinition(registry, "Parallel ForEach", "ploop",
                ("List", list), ("MaxParallelism", 10))
            .AddNodeFromDefinition(registry, "Parallel Collector", "collect",
                ("Key", "results"))
            .AddNodeFromDefinition(registry, "Marker", "end")
            .ConnectExecution("start", "Exit", "ploop", "Enter")
            .ConnectExecution("ploop", "LoopPath", "collect", "Enter")
            .ConnectData("ploop", "Item", "collect", "Value")
            .ConnectExecution("ploop", "Exit", "end", "Enter")
            .Build();

        var context = new NodeRuntimeStorage();
        context.SetVariable("results", new ConcurrentBag<object?>());
        await service.ExecuteAsync(nodes, connections, context, null!,
            NodeExecutionOptions.Default, CancellationToken.None);

        var collected = context.GetVariable("results") as ConcurrentBag<object?>;
        Assert.NotNull(collected);
        var items = collected!.Where(x => x is not null).Cast<object>().Select(x => x.ToString()!).ToHashSet();

        // All 10 items should be present — no duplicates or missing items due to racing.
        for (int i = 0; i < 10; i++)
        {
            Assert.Contains($"item_{i}", items);
        }

        Assert.Equal(10, collected.Count);
    }

    [Fact]
    public async Task ComposesWithLoadFromDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "NodeEditorTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "a.txt"), "hello");
            File.WriteAllText(Path.Combine(dir, "b.txt"), "world");
            File.WriteAllText(Path.Combine(dir, "c.txt"), "test");

            var service = CreateService(out var registry);
            RegisterTestHelperNodes(registry);

            var (nodes, connections) = new TestGraphBuilder()
                .AddNodeFromDefinition(registry, "Start", "start")
                .AddNodeFromDefinition(registry, "Load From Directory", "load",
                    ("DirectoryPath", dir), ("Filter", "*.txt"))
                .AddNodeFromDefinition(registry, "Parallel ForEach", "ploop",
                    ("MaxParallelism", 3))
                .AddNodeFromDefinition(registry, "Parallel Collector", "collect",
                    ("Key", "paths"))
                .AddNodeFromDefinition(registry, "Marker", "end")
                .ConnectExecution("start", "Exit", "load", "Enter")
                .ConnectExecution("load", "Exit", "ploop", "Enter")
                .ConnectData("load", "Files", "ploop", "List")
                .ConnectExecution("ploop", "LoopPath", "collect", "Enter")
                .ConnectData("ploop", "Item", "collect", "Value")
                .ConnectExecution("ploop", "Exit", "end", "Enter")
                .Build();

            var context = new NodeRuntimeStorage();
            context.SetVariable("paths", new ConcurrentBag<object?>());
            await service.ExecuteAsync(nodes, connections, context, null!,
                NodeExecutionOptions.Default, CancellationToken.None);

            Assert.True(context.IsNodeExecuted("end"));

            var collected = context.GetVariable("paths") as ConcurrentBag<object?>;
            Assert.NotNull(collected);
            Assert.Equal(3, collected!.Count);

            var paths = collected.Cast<object>().Select(x => x.ToString()!).ToHashSet();
            Assert.All(paths, p => Assert.EndsWith(".txt", p));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task MaxParallelism_ClampedToOne_WhenZeroOrNegative()
    {
        var service = CreateService(out var registry);
        RegisterTestHelperNodes(registry);

        var list = new SerializableList();
        list.Add("A");
        list.Add("B");

        var (nodes, connections) = new TestGraphBuilder()
            .AddNodeFromDefinition(registry, "Start", "start")
            .AddNodeFromDefinition(registry, "Parallel ForEach", "ploop",
                ("List", list), ("MaxParallelism", 0))
            .AddNodeFromDefinition(registry, "Parallel Collector", "collect",
                ("Key", "items"))
            .AddNodeFromDefinition(registry, "Marker", "end")
            .ConnectExecution("start", "Exit", "ploop", "Enter")
            .ConnectExecution("ploop", "LoopPath", "collect", "Enter")
            .ConnectData("ploop", "Item", "collect", "Value")
            .ConnectExecution("ploop", "Exit", "end", "Enter")
            .Build();

        var context = new NodeRuntimeStorage();
        context.SetVariable("items", new ConcurrentBag<object?>());
        await service.ExecuteAsync(nodes, connections, context, null!,
            NodeExecutionOptions.Default, CancellationToken.None);

        Assert.True(context.IsNodeExecuted("end"));
        var collected = context.GetVariable("items") as ConcurrentBag<object?>;
        Assert.Equal(2, collected!.Count);
    }

    [Fact]
    public async Task IndexOutput_IsCorrectPerIteration()
    {
        var service = CreateService(out var registry);

        // Collector that captures the Index from the parallel loop
        var indexCollectorDef = NodeBuilder.Create("Index Collector")
            .Category("Test").Callable()
            .Input<object>("Value")
            .Input<string>("Key", "indices")
            .OnExecute(async (ctx, ct) =>
            {
                var key = ctx.GetInput<string>("Key");
                var value = ctx.GetInput<object>("Value");

                if (ctx.GetVariable(key) is not ConcurrentBag<object?> bag)
                {
                    bag = new ConcurrentBag<object?>();
                    ctx.SetVariable(key, bag);
                }

                bag.Add(value);
                await ctx.TriggerAsync("Exit");
            })
            .Build();
        registry.RegisterDefinitions(new[] { indexCollectorDef });

        var list = new SerializableList();
        list.Add("A");
        list.Add("B");
        list.Add("C");

        var (nodes, connections) = new TestGraphBuilder()
            .AddNodeFromDefinition(registry, "Start", "start")
            .AddNodeFromDefinition(registry, "Parallel ForEach", "ploop",
                ("List", list), ("MaxParallelism", 3))
            .AddNodeFromDefinition(registry, "Index Collector", "collect",
                ("Key", "indices"))
            .AddNodeFromDefinition(registry, "Marker", "end")
            .ConnectExecution("start", "Exit", "ploop", "Enter")
            .ConnectExecution("ploop", "LoopPath", "collect", "Enter")
            .ConnectData("ploop", "Index", "collect", "Value")
            .ConnectExecution("ploop", "Exit", "end", "Enter")
            .Build();

        var context = new NodeRuntimeStorage();
        context.SetVariable("indices", new ConcurrentBag<object?>());
        await service.ExecuteAsync(nodes, connections, context, null!,
            NodeExecutionOptions.Default, CancellationToken.None);

        Assert.True(context.IsNodeExecuted("end"));

        var collected = context.GetVariable("indices") as ConcurrentBag<object?>;
        Assert.NotNull(collected);
        var indices = collected!.Cast<object>().Select(x => Convert.ToInt32(x)).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { 0, 1, 2 }, indices);
    }
}
