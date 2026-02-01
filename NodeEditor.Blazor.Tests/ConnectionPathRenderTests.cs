using System.Collections.ObjectModel;
using Bunit;
using NodeEditor.Blazor.Components;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.ViewModels;

namespace NodeEditor.Blazor.Tests;

public sealed class ConnectionPathRenderTests : BunitContext
{
    [Fact]
    public void ConnectionPath_RendersWhenEndpointsMove()
    {
        var (nodes, connection) = CreateGraph();

        var cut = Render<ConnectionPath>(parameters => parameters
            .Add(p => p.Connection, connection)
            .Add(p => p.Nodes, nodes));

        var initialRenderCount = cut.RenderCount;

        nodes[1].Position = new Point2D(300, 0);
        cut.Render();

        Assert.Equal(initialRenderCount + 1, cut.RenderCount);
    }

    [Fact]
    public void ConnectionPath_DoesNotRenderWhenUnchanged()
    {
        var (nodes, connection) = CreateGraph();

        var cut = Render<ConnectionPath>(parameters => parameters
            .Add(p => p.Connection, connection)
            .Add(p => p.Nodes, nodes));

        var initialMarkup = cut.Markup;
        cut.Render();

        cut.MarkupMatches(initialMarkup);
    }

    private static (ObservableCollection<NodeViewModel> nodes, ConnectionData connection) CreateGraph()
    {
        var outputSocket = new SocketData("Out", "int", false, false);
        var inputSocket = new SocketData("In", "int", true, false);

        var nodeA = new NodeViewModel(new NodeData(
            "n1",
            "A",
            false,
            false,
            Array.Empty<SocketData>(),
            new[] { outputSocket }));
        var nodeB = new NodeViewModel(new NodeData(
            "n2",
            "B",
            false,
            false,
            new[] { inputSocket },
            Array.Empty<SocketData>()));

        nodeA.Position = new Point2D(0, 0);
        nodeB.Position = new Point2D(200, 0);

        var nodes = new ObservableCollection<NodeViewModel> { nodeA, nodeB };
        var connection = new ConnectionData("n1", "n2", "Out", "In", false);

        return (nodes, connection);
    }
}