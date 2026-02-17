using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Blazor.Tests;

public sealed class LoadFromDirectoryNodeTests
{
    private static NodeExecutionService CreateService(out NodeRegistryService registry)
    {
        registry = new NodeRegistryService(new NodeDiscoveryService());
        registry.EnsureInitialized();
        return new NodeExecutionService(new ExecutionPlanner(), registry, new MinimalServiceProvider());
    }

    private static NodeData NodeFromDef(NodeRegistryService registry, string defName, string id,
        params (string socketName, object value)[] inputOverrides)
    {
        var def = registry.Definitions.First(d => d.Name == defName
            && (d.NodeType is not null || d.InlineExecutor is not null));
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

    [Fact]
    public async Task EnumeratesFiles_HappyPath()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "a.txt"), "hello");
            File.WriteAllText(Path.Combine(dir, "b.txt"), "world");
            File.WriteAllText(Path.Combine(dir, "c.png"), "image");

            var service = CreateService(out var registry);
            var start = NodeFromDef(registry, "Start", "start");
            var load = NodeFromDef(registry, "Load From Directory", "load",
                ("DirectoryPath", dir), ("Filter", "*"));
            var end = NodeFromDef(registry, "Marker", "end");

            var nodes = new List<NodeData> { start, load, end };
            var connections = new List<ConnectionData>
            {
                TestConnections.Exec("start", "Exit", "load", "Enter"),
                TestConnections.Exec("load", "Exit", "end", "Enter")
            };

            var context = new NodeRuntimeStorage();
            await service.ExecuteAsync(nodes, connections, context, null!,
                NodeExecutionOptions.Default, CancellationToken.None);

            var files = context.GetSocketValue("load", "Files") as SerializableList;
            Assert.NotNull(files);
            Assert.Equal(3, files.Count);
            Assert.Equal(3, context.GetSocketValue("load", "Count"));
            Assert.True((bool)context.GetSocketValue("load", "Ok")!);
            Assert.Equal(string.Empty, context.GetSocketValue("load", "Error"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task FiltersByWildcard_TxtOnly()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "a.txt"), "hello");
            File.WriteAllText(Path.Combine(dir, "b.txt"), "world");
            File.WriteAllText(Path.Combine(dir, "c.png"), "image");

            var service = CreateService(out var registry);
            var start = NodeFromDef(registry, "Start", "start");
            var load = NodeFromDef(registry, "Load From Directory", "load",
                ("DirectoryPath", dir), ("Filter", "*.txt"));
            var end = NodeFromDef(registry, "Marker", "end");

            var nodes = new List<NodeData> { start, load, end };
            var connections = new List<ConnectionData>
            {
                TestConnections.Exec("start", "Exit", "load", "Enter"),
                TestConnections.Exec("load", "Exit", "end", "Enter")
            };

            var context = new NodeRuntimeStorage();
            await service.ExecuteAsync(nodes, connections, context, null!,
                NodeExecutionOptions.Default, CancellationToken.None);

            var files = context.GetSocketValue("load", "Files") as SerializableList;
            Assert.NotNull(files);
            Assert.Equal(2, files.Count);

            var paths = files.Snapshot().Cast<string>().ToArray();
            Assert.All(paths, p => Assert.EndsWith(".txt", p));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task FiltersByWildcard_PrefixPattern()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "image_01.png"), "a");
            File.WriteAllText(Path.Combine(dir, "image_02.png"), "b");
            File.WriteAllText(Path.Combine(dir, "document.pdf"), "c");

            var service = CreateService(out var registry);
            var start = NodeFromDef(registry, "Start", "start");
            var load = NodeFromDef(registry, "Load From Directory", "load",
                ("DirectoryPath", dir), ("Filter", "image*"));
            var end = NodeFromDef(registry, "Marker", "end");

            var nodes = new List<NodeData> { start, load, end };
            var connections = new List<ConnectionData>
            {
                TestConnections.Exec("start", "Exit", "load", "Enter"),
                TestConnections.Exec("load", "Exit", "end", "Enter")
            };

            var context = new NodeRuntimeStorage();
            await service.ExecuteAsync(nodes, connections, context, null!,
                NodeExecutionOptions.Default, CancellationToken.None);

            var files = context.GetSocketValue("load", "Files") as SerializableList;
            Assert.NotNull(files);
            Assert.Equal(2, files.Count);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task EmptyDirectory_ReturnsEmptyList()
    {
        var dir = CreateTempDir();
        try
        {
            var service = CreateService(out var registry);
            var start = NodeFromDef(registry, "Start", "start");
            var load = NodeFromDef(registry, "Load From Directory", "load",
                ("DirectoryPath", dir));
            var end = NodeFromDef(registry, "Marker", "end");

            var nodes = new List<NodeData> { start, load, end };
            var connections = new List<ConnectionData>
            {
                TestConnections.Exec("start", "Exit", "load", "Enter"),
                TestConnections.Exec("load", "Exit", "end", "Enter")
            };

            var context = new NodeRuntimeStorage();
            await service.ExecuteAsync(nodes, connections, context, null!,
                NodeExecutionOptions.Default, CancellationToken.None);

            var files = context.GetSocketValue("load", "Files") as SerializableList;
            Assert.NotNull(files);
            Assert.Equal(0, files.Count);
            Assert.True((bool)context.GetSocketValue("load", "Ok")!);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task MissingDirectory_SetsError()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var service = CreateService(out var registry);
        var start = NodeFromDef(registry, "Start", "start");
        var load = NodeFromDef(registry, "Load From Directory", "load",
            ("DirectoryPath", dir));
        var end = NodeFromDef(registry, "Marker", "end");

        var nodes = new List<NodeData> { start, load, end };
        var connections = new List<ConnectionData>
        {
            TestConnections.Exec("start", "Exit", "load", "Enter"),
            TestConnections.Exec("load", "Exit", "end", "Enter")
        };

        var context = new NodeRuntimeStorage();
        await service.ExecuteAsync(nodes, connections, context, null!,
            NodeExecutionOptions.Default, CancellationToken.None);

        Assert.False((bool)context.GetSocketValue("load", "Ok")!);
        var error = context.GetSocketValue("load", "Error") as string;
        Assert.Contains("not found", error!);
        Assert.True(context.IsNodeExecuted("end"), "Exit should still fire on error");
    }

    [Fact]
    public async Task EmptyDirectoryPath_SetsError()
    {
        var service = CreateService(out var registry);
        var start = NodeFromDef(registry, "Start", "start");
        var load = NodeFromDef(registry, "Load From Directory", "load",
            ("DirectoryPath", ""));
        var end = NodeFromDef(registry, "Marker", "end");

        var nodes = new List<NodeData> { start, load, end };
        var connections = new List<ConnectionData>
        {
            TestConnections.Exec("start", "Exit", "load", "Enter"),
            TestConnections.Exec("load", "Exit", "end", "Enter")
        };

        var context = new NodeRuntimeStorage();
        await service.ExecuteAsync(nodes, connections, context, null!,
            NodeExecutionOptions.Default, CancellationToken.None);

        Assert.False((bool)context.GetSocketValue("load", "Ok")!);
        var error = context.GetSocketValue("load", "Error") as string;
        Assert.Contains("required", error!);
    }

    [Fact]
    public async Task Recursive_FindsSubdirectoryFiles()
    {
        var dir = CreateTempDir();
        try
        {
            var sub = Path.Combine(dir, "subdir");
            Directory.CreateDirectory(sub);
            File.WriteAllText(Path.Combine(dir, "top.txt"), "a");
            File.WriteAllText(Path.Combine(sub, "nested.txt"), "b");

            var service = CreateService(out var registry);
            var start = NodeFromDef(registry, "Start", "start");
            var load = NodeFromDef(registry, "Load From Directory", "load",
                ("DirectoryPath", dir), ("Filter", "*.txt"), ("Recursive", true));
            var end = NodeFromDef(registry, "Marker", "end");

            var nodes = new List<NodeData> { start, load, end };
            var connections = new List<ConnectionData>
            {
                TestConnections.Exec("start", "Exit", "load", "Enter"),
                TestConnections.Exec("load", "Exit", "end", "Enter")
            };

            var context = new NodeRuntimeStorage();
            await service.ExecuteAsync(nodes, connections, context, null!,
                NodeExecutionOptions.Default, CancellationToken.None);

            var files = context.GetSocketValue("load", "Files") as SerializableList;
            Assert.NotNull(files);
            Assert.Equal(2, files.Count);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task NonRecursive_DoesNotFindSubdirectoryFiles()
    {
        var dir = CreateTempDir();
        try
        {
            var sub = Path.Combine(dir, "subdir");
            Directory.CreateDirectory(sub);
            File.WriteAllText(Path.Combine(dir, "top.txt"), "a");
            File.WriteAllText(Path.Combine(sub, "nested.txt"), "b");

            var service = CreateService(out var registry);
            var start = NodeFromDef(registry, "Start", "start");
            var load = NodeFromDef(registry, "Load From Directory", "load",
                ("DirectoryPath", dir), ("Filter", "*.txt"), ("Recursive", false));
            var end = NodeFromDef(registry, "Marker", "end");

            var nodes = new List<NodeData> { start, load, end };
            var connections = new List<ConnectionData>
            {
                TestConnections.Exec("start", "Exit", "load", "Enter"),
                TestConnections.Exec("load", "Exit", "end", "Enter")
            };

            var context = new NodeRuntimeStorage();
            await service.ExecuteAsync(nodes, connections, context, null!,
                NodeExecutionOptions.Default, CancellationToken.None);

            var files = context.GetSocketValue("load", "Files") as SerializableList;
            Assert.NotNull(files);
            Assert.Equal(1, files.Count);

            var path = files.Snapshot()[0] as string;
            Assert.Contains("top.txt", path!);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task ComposesWithForEachLoop()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "a.txt"), "hello");
            File.WriteAllText(Path.Combine(dir, "b.txt"), "world");

            var service = CreateService(out var registry);

            // Register a collector inline node
            var collectorDef = NodeBuilder.Create("Test File Collector")
                .Category("Test").Callable()
                .Input<object>("Value")
                .Input<string>("Key", "files")
                .OnExecute(async (ctx, ct) =>
                {
                    var key = ctx.GetInput<string>("Key");
                    var value = ctx.GetInput<object>("Value");

                    if (ctx.GetVariable(key) is not System.Collections.Concurrent.ConcurrentQueue<object?> queue)
                    {
                        queue = new System.Collections.Concurrent.ConcurrentQueue<object?>();
                        ctx.SetVariable(key, queue);
                    }

                    queue.Enqueue(value);
                    await ctx.TriggerAsync("Exit");
                })
                .Build();
            registry.RegisterDefinitions(new[] { collectorDef });

            var (nodes, connections) = new TestGraphBuilder()
                .AddNodeFromDefinition(registry, "Start", "start")
                .AddNodeFromDefinition(registry, "Load From Directory", "load",
                    ("DirectoryPath", dir), ("Filter", "*.txt"))
                .AddNodeFromDefinition(registry, "ForEach Loop", "loop")
                .AddNodeFromDefinition(registry, "Test File Collector", "collect", ("Key", "files"))
                .AddNodeFromDefinition(registry, "Marker", "end")
                .ConnectExecution("start", "Exit", "load", "Enter")
                .ConnectExecution("load", "Exit", "loop", "Enter")
                .ConnectData("load", "Files", "loop", "List")
                .ConnectExecution("loop", "LoopPath", "collect", "Enter")
                .ConnectData("loop", "Obj", "collect", "Value")
                .ConnectExecution("loop", "Exit", "end", "Enter")
                .Build();

            var context = new NodeRuntimeStorage();
            await service.ExecuteAsync(nodes, connections, context, null!,
                NodeExecutionOptions.Default, CancellationToken.None);

            Assert.True(context.IsNodeExecuted("end"));

            var collected = context.GetVariable("files") as System.Collections.Concurrent.ConcurrentQueue<object?>;
            Assert.NotNull(collected);
            Assert.Equal(2, collected.Count);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "NodeEditorTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
