using NodeEditor.Net.Services.Execution;

namespace NodeEditor.Blazor.Tests;

public sealed class LayeredRuntimeStorageTests
{
    [Fact]
    public void SocketValue_WriteLocal_DoesNotAffectParent()
    {
        var parent = new NodeRuntimeStorage();
        parent.SetSocketValue("node1", "out", "parentValue");

        var layer = new LayeredRuntimeStorage(parent);
        layer.SetSocketValue("node1", "out", "childValue");

        Assert.Equal("childValue", layer.GetSocketValue("node1", "out"));
        Assert.Equal("parentValue", parent.GetSocketValue("node1", "out"));
    }

    [Fact]
    public void SocketValue_ReadThrough_ReturnsParentValue()
    {
        var parent = new NodeRuntimeStorage();
        parent.SetSocketValue("node1", "out", "parentValue");

        var layer = new LayeredRuntimeStorage(parent);

        Assert.Equal("parentValue", layer.GetSocketValue("node1", "out"));
    }

    [Fact]
    public void SocketValue_TryGet_ReadThrough()
    {
        var parent = new NodeRuntimeStorage();
        parent.SetSocketValue("node1", "out", 42);

        var layer = new LayeredRuntimeStorage(parent);
        var found = layer.TryGetSocketValue("node1", "out", out var value);

        Assert.True(found);
        Assert.Equal(42, value);
    }

    [Fact]
    public void SocketValue_TryGet_LocalOverridesParent()
    {
        var parent = new NodeRuntimeStorage();
        parent.SetSocketValue("node1", "out", 42);

        var layer = new LayeredRuntimeStorage(parent);
        layer.SetSocketValue("node1", "out", 99);

        var found = layer.TryGetSocketValue("node1", "out", out var value);
        Assert.True(found);
        Assert.Equal(99, value);
    }

    [Fact]
    public void SocketValue_TryGet_NotFound()
    {
        var parent = new NodeRuntimeStorage();
        var layer = new LayeredRuntimeStorage(parent);

        var found = layer.TryGetSocketValue("node1", "missing", out _);
        Assert.False(found);
    }

    [Fact]
    public void ExecutionTracking_FullyLocal()
    {
        var parent = new NodeRuntimeStorage();
        parent.MarkNodeExecuted("node1");

        var layer = new LayeredRuntimeStorage(parent);

        // Layer does NOT see parent's execution marks
        Assert.False(layer.IsNodeExecuted("node1"));

        layer.MarkNodeExecuted("node2");
        Assert.True(layer.IsNodeExecuted("node2"));
        Assert.False(parent.IsNodeExecuted("node2"));
    }

    [Fact]
    public void ExecutionTracking_ClearLocal()
    {
        var parent = new NodeRuntimeStorage();
        var layer = new LayeredRuntimeStorage(parent);

        layer.MarkNodeExecuted("node1");
        Assert.True(layer.IsNodeExecuted("node1"));

        layer.ClearNodeExecuted("node1");
        Assert.False(layer.IsNodeExecuted("node1"));
    }

    [Fact]
    public void ExecutionTracking_ClearForNodes()
    {
        var parent = new NodeRuntimeStorage();
        var layer = new LayeredRuntimeStorage(parent);

        layer.MarkNodeExecuted("a");
        layer.MarkNodeExecuted("b");
        layer.MarkNodeExecuted("c");

        layer.ClearExecutedForNodes(new[] { "a", "c" });

        Assert.False(layer.IsNodeExecuted("a"));
        Assert.True(layer.IsNodeExecuted("b"));
        Assert.False(layer.IsNodeExecuted("c"));
    }

    [Fact]
    public void Variables_ReadThrough()
    {
        var parent = new NodeRuntimeStorage();
        parent.SetVariable("key1", "parentVal");

        var layer = new LayeredRuntimeStorage(parent);

        Assert.Equal("parentVal", layer.GetVariable("key1"));
    }

    [Fact]
    public void Variables_WriteLocal_DoesNotAffectParent()
    {
        var parent = new NodeRuntimeStorage();
        parent.SetVariable("key1", "parentVal");

        var layer = new LayeredRuntimeStorage(parent);
        layer.SetVariable("key1", "childVal");

        Assert.Equal("childVal", layer.GetVariable("key1"));
        Assert.Equal("parentVal", parent.GetVariable("key1"));
    }

    [Fact]
    public void Variables_WriteNewKey_DoesNotPropagateToParent()
    {
        var parent = new NodeRuntimeStorage();
        var layer = new LayeredRuntimeStorage(parent);

        layer.SetVariable("newKey", "newVal");

        Assert.Equal("newVal", layer.GetVariable("newKey"));
        Assert.Null(parent.GetVariable("newKey"));
    }

    [Fact]
    public void EventBus_SharedFromParent()
    {
        var parent = new NodeRuntimeStorage();
        var layer = new LayeredRuntimeStorage(parent);

        Assert.Same(parent.EventBus, layer.EventBus);
    }

    [Fact]
    public void NestedChildren_ReadThrough()
    {
        var parent = new NodeRuntimeStorage();
        parent.SetSocketValue("node1", "out", "root");

        var layer1 = new LayeredRuntimeStorage(parent);
        var layer2 = layer1.CreateChild("nested") as LayeredRuntimeStorage;

        Assert.NotNull(layer2);
        Assert.Equal("root", layer2!.GetSocketValue("node1", "out"));
    }

    [Fact]
    public void NestedChildren_WriteIsolation()
    {
        var parent = new NodeRuntimeStorage();
        parent.SetSocketValue("node1", "out", "root");

        var layer1 = new LayeredRuntimeStorage(parent);
        layer1.SetSocketValue("node1", "out", "layer1");

        var layer2 = layer1.CreateChild("nested");
        layer2.SetSocketValue("node1", "out", "layer2");

        Assert.Equal("root", parent.GetSocketValue("node1", "out"));
        Assert.Equal("layer1", layer1.GetSocketValue("node1", "out"));
        Assert.Equal("layer2", layer2.GetSocketValue("node1", "out"));
    }

    [Fact]
    public void ConcurrentWritesDoNotCorrupt()
    {
        var parent = new NodeRuntimeStorage();
        var layer = new LayeredRuntimeStorage(parent);

        var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(() =>
        {
            layer.SetSocketValue("node1", $"socket_{i}", i);
            layer.MarkNodeExecuted($"node_{i}");
            layer.SetVariable($"var_{i}", i);
        })).ToArray();

        Task.WaitAll(tasks);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(i, layer.GetSocketValue("node1", $"socket_{i}"));
            Assert.True(layer.IsNodeExecuted($"node_{i}"));
            Assert.Equal(i, layer.GetVariable($"var_{i}"));
        }
    }
}
