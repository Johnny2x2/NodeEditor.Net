using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services;

namespace NodeEditor.Blazor.Tests;

public sealed class NodeEditorStateTests
{
    [Fact]
    public void NodeEditorState_StartsEmpty()
    {
        var state = new NodeEditorState();

        Assert.Empty(state.Nodes);
        Assert.Empty(state.Connections);
        Assert.Empty(state.SelectedNodeIds);
        Assert.Null(state.SelectedConnection);
    }

    [Fact]
    public void NodeEditorState_DefaultViewport_IsEmptyRect()
    {
        var state = new NodeEditorState();

        Assert.Equal(new Rect2D(0, 0, 0, 0), state.Viewport);
    }

    [Fact]
    public void RemoveConnectionsToInput_RemovesMatchingConnections()
    {
        var state = new NodeEditorState();

        var c1 = new ConnectionData("A", "B", "Out", "In1", false);
        var c2 = new ConnectionData("A", "B", "Out", "In2", false);

        state.AddConnection(c1);
        state.AddConnection(c2);

        state.RemoveConnectionsToInput("B", "In1");

        Assert.DoesNotContain(c1, state.Connections);
        Assert.Contains(c2, state.Connections);
    }
}
