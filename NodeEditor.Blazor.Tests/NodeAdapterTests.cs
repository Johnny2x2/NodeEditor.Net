using NodeEditor.Blazor.Adapters;
using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Tests;

public sealed class NodeAdapterTests
{
    [Fact]
    public void FromSnapshot_MapsSocketsAndValues()
    {
        var input = new LegacySocketSnapshot(
            "In",
            "System.Int32",
            true,
            false,
            SocketValue.FromObject(7));

        var output = new LegacySocketSnapshot(
            "Out",
            "System.Int32",
            false,
            false,
            SocketValue.FromObject(9));

        var snapshot = new LegacyNodeSnapshot(
            "node-1",
            "Test",
            false,
            false,
            new[] { input },
            new[] { output });

        var node = NodeAdapter.FromSnapshot(snapshot);

        Assert.Equal("node-1", node.Id);
        Assert.Single(node.Inputs);
        Assert.Single(node.Outputs);
        Assert.Equal("In", node.Inputs[0].Name);
        Assert.Equal("Out", node.Outputs[0].Name);
        Assert.Equal(7, node.Inputs[0].Value?.ToObject<int>());
        Assert.Equal(9, node.Outputs[0].Value?.ToObject<int>());
    }
}
