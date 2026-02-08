using Bunit;
using NodeEditor.Blazor.Components;
using NodeEditor.Net.Models;
using NodeEditor.Net.ViewModels;

namespace NodeEditor.Blazor.Tests;

public sealed class NodeComponentRenderTests : BunitContext
{
    [Fact]
    public void NodeComponent_RendersWhenNodeChanges()
    {
        var node = CreateNode("n1");
        var connections = new List<ConnectionData>();

        var cut = Render<NodeComponent>(parameters => parameters
            .Add(p => p.Node, node)
            .Add(p => p.Connections, connections));

        var initialRenderCount = cut.RenderCount;

        node.Position = new Point2D(10, 10);
        cut.Render();

        Assert.Equal(initialRenderCount + 1, cut.RenderCount);
    }

    [Fact]
    public void NodeComponent_DoesNotRenderWhenUnchanged()
    {
        var node = CreateNode("n1");
        var connections = new List<ConnectionData>();

        var cut = Render<NodeComponent>(parameters => parameters
            .Add(p => p.Node, node)
            .Add(p => p.Connections, connections));

        var initialMarkup = cut.Markup;
        cut.Render();

        cut.MarkupMatches(initialMarkup);
    }

    [Fact]
    public void NodeComponent_RendersWhenConnectionsChange()
    {
        var node = CreateNode("n1");
        var connections = new List<ConnectionData>();

        var cut = Render<NodeComponent>(parameters => parameters
            .Add(p => p.Node, node)
            .Add(p => p.Connections, connections));

        var initialRenderCount = cut.RenderCount;

        connections.Add(new ConnectionData("n1", "n2", "out", "in", false));
        cut.Render();

        Assert.Equal(initialRenderCount + 1, cut.RenderCount);
    }

    private static NodeViewModel CreateNode(string id)
    {
        var node = new NodeViewModel(new NodeData(
            id,
            "Node",
            false,
            false,
            Array.Empty<SocketData>(),
            Array.Empty<SocketData>()));

        node.Position = new Point2D(0, 0);
        node.Size = new Size2D(100, 60);
        return node;
    }
}