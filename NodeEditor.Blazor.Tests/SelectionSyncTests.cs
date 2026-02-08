using NodeEditor.Net.Models;
using NodeEditor.Net.Services;
using NodeEditor.Net.ViewModels;

namespace NodeEditor.Blazor.Tests;

public sealed class SelectionSyncTests
{
    [Fact]
    public void SelectingNode_SetsViewModelFlag()
    {
        var state = new NodeEditorState();
        var node = new NodeViewModel(new NodeData("n1", "Test", false, false, Array.Empty<SocketData>(), Array.Empty<SocketData>()));
        state.AddNode(node);

        state.SelectNode("n1");

        Assert.True(node.IsSelected);
        Assert.Contains("n1", state.SelectedNodeIds);
    }

    [Fact]
    public void ClearingSelection_ResetsViewModelFlags()
    {
        var state = new NodeEditorState();
        var node = new NodeViewModel(new NodeData("n1", "Test", false, false, Array.Empty<SocketData>(), Array.Empty<SocketData>()))
        {
            IsSelected = true
        };
        state.AddNode(node);
        state.SelectedNodeIds.Add("n1");

        state.ClearSelection();

        Assert.False(node.IsSelected);
        Assert.Empty(state.SelectedNodeIds);
    }
}
