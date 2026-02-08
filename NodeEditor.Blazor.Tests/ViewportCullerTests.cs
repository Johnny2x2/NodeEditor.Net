using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services;
using NodeEditor.Blazor.ViewModels;

namespace NodeEditor.Blazor.Tests;

public sealed class ViewportCullerTests
{
    [Fact]
    public void GetVisibleNodes_ReturnsOnlyNodesInsideViewport()
    {
        var converter = new CoordinateConverter();
        var culler = new ViewportCuller(converter);
        var nodes = new List<NodeViewModel>
        {
            CreateNode("n1", new Point2D(10, 10), new Size2D(50, 50)),
            CreateNode("n2", new Point2D(200, 200), new Size2D(50, 50))
        };

        var visible = culler.GetVisibleNodes(nodes, new Rect2D(0, 0, 100, 100));

        Assert.Single(visible);
        Assert.Equal("n1", visible[0].Data.Id);
    }

    [Fact]
    public void GetVisibleNodes_AlwaysIncludeNodeIds_ReturnsForcedNodes()
    {
        var converter = new CoordinateConverter();
        var culler = new ViewportCuller(converter);
        var nodes = new List<NodeViewModel>
        {
            CreateNode("n1", new Point2D(10, 10), new Size2D(50, 50)),
            CreateNode("n2", new Point2D(200, 200), new Size2D(50, 50))
        };

        var visible = culler.GetVisibleNodes(nodes, new Rect2D(0, 0, 100, 100), new[] { "n2" });

        Assert.Equal(2, visible.Count);
        Assert.Contains(visible, n => n.Data.Id == "n2");
    }

    [Fact]
    public void GetVisibleConnections_ReturnsConnectionsWithVisibleEndpoints()
    {
        var converter = new CoordinateConverter();
        var culler = new ViewportCuller(converter);
        var nodes = new List<NodeViewModel>
        {
            CreateNode("n1", new Point2D(10, 10), new Size2D(50, 50)),
            CreateNode("n2", new Point2D(200, 200), new Size2D(50, 50)),
            CreateNode("n3", new Point2D(300, 300), new Size2D(50, 50))
        };

        var connections = new List<ConnectionData>
        {
            new("n1", "n2", "out", "in", false),
            new("n2", "n3", "out", "in", false)
        };

        var visibleNodes = culler.GetVisibleNodes(nodes, new Rect2D(0, 0, 100, 100));
        var visibleConnections = culler.GetVisibleConnections(connections, visibleNodes);

        Assert.Single(visibleConnections);
        Assert.Equal("n1", visibleConnections[0].OutputNodeId);
    }

    private static NodeViewModel CreateNode(string id, Point2D position, Size2D size)
    {
        var node = new NodeViewModel(new NodeData(
            id,
            "Node",
            false,
            false,
            Array.Empty<SocketData>(),
            Array.Empty<SocketData>()));

        node.Position = position;
        node.Size = size;
        return node;
    }
}