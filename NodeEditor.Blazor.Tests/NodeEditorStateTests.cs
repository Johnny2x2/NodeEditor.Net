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
    }

    [Fact]
    public void NodeEditorState_DefaultViewport_IsEmptyRect()
    {
        var state = new NodeEditorState();

        Assert.Equal(new Rect2D(0, 0, 0, 0), state.Viewport);
    }
}
