using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services;
using NodeEditor.Blazor.ViewModels;

namespace NodeEditor.Blazor.Tests;

public sealed class StateEventTests
{
    [Fact]
    public void AddNode_RaisesNodeAddedEvent()
    {
        var state = new NodeEditorState();
        var node = new NodeViewModel(new NodeData("n1", "Test", false, false, Array.Empty<SocketData>(), Array.Empty<SocketData>()));
        
        NodeEventArgs? raisedArgs = null;
        state.NodeAdded += (sender, args) => raisedArgs = args;

        state.AddNode(node);

        Assert.NotNull(raisedArgs);
        Assert.Equal(node, raisedArgs.Node);
    }

    [Fact]
    public void RemoveNode_RaisesNodeRemovedEvent()
    {
        var state = new NodeEditorState();
        var node = new NodeViewModel(new NodeData("n1", "Test", false, false, Array.Empty<SocketData>(), Array.Empty<SocketData>()));
        state.AddNode(node);

        NodeEventArgs? raisedArgs = null;
        state.NodeRemoved += (sender, args) => raisedArgs = args;

        state.RemoveNode("n1");

        Assert.NotNull(raisedArgs);
        Assert.Equal(node, raisedArgs.Node);
    }

    [Fact]
    public void RemoveNode_RemovesFromNodesCollection()
    {
        var state = new NodeEditorState();
        var node = new NodeViewModel(new NodeData("n1", "Test", false, false, Array.Empty<SocketData>(), Array.Empty<SocketData>()));
        state.AddNode(node);

        state.RemoveNode("n1");

        Assert.Empty(state.Nodes);
    }

    [Fact]
    public void RemoveNode_RemovesFromSelection()
    {
        var state = new NodeEditorState();
        var node = new NodeViewModel(new NodeData("n1", "Test", false, false, Array.Empty<SocketData>(), Array.Empty<SocketData>()));
        state.AddNode(node);
        state.SelectNode("n1");

        state.RemoveNode("n1");

        Assert.DoesNotContain("n1", state.SelectedNodeIds);
    }

    [Fact]
    public void AddConnection_RaisesConnectionAddedEvent()
    {
        var state = new NodeEditorState();
        var connection = new ConnectionData("n1", "n2", "out1", "in1", false);

        ConnectionEventArgs? raisedArgs = null;
        state.ConnectionAdded += (sender, args) => raisedArgs = args;

        state.AddConnection(connection);

        Assert.NotNull(raisedArgs);
        Assert.Equal(connection, raisedArgs.Connection);
    }

    [Fact]
    public void RemoveConnection_RaisesConnectionRemovedEvent()
    {
        var state = new NodeEditorState();
        var connection = new ConnectionData("n1", "n2", "out1", "in1", false);
        state.AddConnection(connection);

        ConnectionEventArgs? raisedArgs = null;
        state.ConnectionRemoved += (sender, args) => raisedArgs = args;

        state.RemoveConnection(connection);

        Assert.NotNull(raisedArgs);
        Assert.Equal(connection, raisedArgs.Connection);
    }

    [Fact]
    public void SelectNode_RaisesSelectionChangedEvent()
    {
        var state = new NodeEditorState();
        var node = new NodeViewModel(new NodeData("n1", "Test", false, false, Array.Empty<SocketData>(), Array.Empty<SocketData>()));
        state.AddNode(node);

        SelectionChangedEventArgs? raisedArgs = null;
        state.SelectionChanged += (sender, args) => raisedArgs = args;

        state.SelectNode("n1");

        Assert.NotNull(raisedArgs);
        Assert.Empty(raisedArgs.PreviousSelection);
        Assert.Single(raisedArgs.CurrentSelection);
        Assert.Contains("n1", raisedArgs.CurrentSelection);
    }

    [Fact]
    public void SelectNode_WithClearExisting_IncludesPreviousSelectionInEvent()
    {
        var state = new NodeEditorState();
        var node1 = new NodeViewModel(new NodeData("n1", "Test1", false, false, Array.Empty<SocketData>(), Array.Empty<SocketData>()));
        var node2 = new NodeViewModel(new NodeData("n2", "Test2", false, false, Array.Empty<SocketData>(), Array.Empty<SocketData>()));
        state.AddNode(node1);
        state.AddNode(node2);
        state.SelectNode("n1");

        SelectionChangedEventArgs? raisedArgs = null;
        state.SelectionChanged += (sender, args) => raisedArgs = args;

        state.SelectNode("n2", clearExisting: true);

        Assert.NotNull(raisedArgs);
        Assert.Single(raisedArgs.PreviousSelection);
        Assert.Contains("n1", raisedArgs.PreviousSelection);
        Assert.Single(raisedArgs.CurrentSelection);
        Assert.Contains("n2", raisedArgs.CurrentSelection);
    }

    [Fact]
    public void ToggleSelectNode_RaisesSelectionChangedEvent()
    {
        var state = new NodeEditorState();
        var node = new NodeViewModel(new NodeData("n1", "Test", false, false, Array.Empty<SocketData>(), Array.Empty<SocketData>()));
        state.AddNode(node);

        SelectionChangedEventArgs? raisedArgs = null;
        state.SelectionChanged += (sender, args) => raisedArgs = args;

        state.ToggleSelectNode("n1");

        Assert.NotNull(raisedArgs);
        Assert.Empty(raisedArgs.PreviousSelection);
        Assert.Single(raisedArgs.CurrentSelection);
        Assert.Contains("n1", raisedArgs.CurrentSelection);
    }

    [Fact]
    public void ClearSelection_RaisesSelectionChangedEvent()
    {
        var state = new NodeEditorState();
        var node = new NodeViewModel(new NodeData("n1", "Test", false, false, Array.Empty<SocketData>(), Array.Empty<SocketData>()));
        state.AddNode(node);
        state.SelectNode("n1");

        SelectionChangedEventArgs? raisedArgs = null;
        state.SelectionChanged += (sender, args) => raisedArgs = args;

        state.ClearSelection();

        Assert.NotNull(raisedArgs);
        Assert.Single(raisedArgs.PreviousSelection);
        Assert.Contains("n1", raisedArgs.PreviousSelection);
        Assert.Empty(raisedArgs.CurrentSelection);
    }

    [Fact]
    public void SetZoom_RaisesZoomChangedEvent()
    {
        var state = new NodeEditorState();

        ZoomChangedEventArgs? raisedArgs = null;
        state.ZoomChanged += (sender, args) => raisedArgs = args;

        state.Zoom = 2.0;

        Assert.NotNull(raisedArgs);
        Assert.Equal(1.0, raisedArgs.PreviousZoom);
        Assert.Equal(2.0, raisedArgs.CurrentZoom);
    }

    [Fact]
    public void SetZoom_SameValue_DoesNotRaiseEvent()
    {
        var state = new NodeEditorState();
        state.Zoom = 2.0;

        var eventRaised = false;
        state.ZoomChanged += (sender, args) => eventRaised = true;

        state.Zoom = 2.0;

        Assert.False(eventRaised);
    }

    [Fact]
    public void SetViewport_RaisesViewportChangedEvent()
    {
        var state = new NodeEditorState();
        var newViewport = new Rect2D(10, 20, 100, 200);

        ViewportChangedEventArgs? raisedArgs = null;
        state.ViewportChanged += (sender, args) => raisedArgs = args;

        state.Viewport = newViewport;

        Assert.NotNull(raisedArgs);
        Assert.Equal(new Rect2D(0, 0, 0, 0), raisedArgs.PreviousViewport);
        Assert.Equal(newViewport, raisedArgs.CurrentViewport);
    }

    [Fact]
    public void SetViewport_SameValue_DoesNotRaiseEvent()
    {
        var state = new NodeEditorState();
        var viewport = new Rect2D(10, 20, 100, 200);
        state.Viewport = viewport;

        var eventRaised = false;
        state.ViewportChanged += (sender, args) => eventRaised = true;

        state.Viewport = viewport;

        Assert.False(eventRaised);
    }

    [Fact]
    public void MultipleSubscribers_AllReceiveEvents()
    {
        var state = new NodeEditorState();
        var node = new NodeViewModel(new NodeData("n1", "Test", false, false, Array.Empty<SocketData>(), Array.Empty<SocketData>()));

        var subscriber1Called = false;
        var subscriber2Called = false;

        state.NodeAdded += (sender, args) => subscriber1Called = true;
        state.NodeAdded += (sender, args) => subscriber2Called = true;

        state.AddNode(node);

        Assert.True(subscriber1Called);
        Assert.True(subscriber2Called);
    }

    [Fact]
    public void UnsubscribedHandlers_DoNotReceiveEvents()
    {
        var state = new NodeEditorState();
        var node = new NodeViewModel(new NodeData("n1", "Test", false, false, Array.Empty<SocketData>(), Array.Empty<SocketData>()));

        var eventRaised = false;
        EventHandler<NodeEventArgs> handler = (sender, args) => eventRaised = true;

        state.NodeAdded += handler;
        state.NodeAdded -= handler;

        state.AddNode(node);

        Assert.False(eventRaised);
    }
}
