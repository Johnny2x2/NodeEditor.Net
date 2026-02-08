using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services;
using NodeEditor.Blazor.ViewModels;

namespace NodeEditor.Blazor.Tests;

public sealed class NodeEditorStateBridgeTests
{
    [Fact]
    public void Bridge_StartsUnattached()
    {
        var bridge = new NodeEditorStateBridge();

        Assert.Null(bridge.Current);
        Assert.False(bridge.IsAttached);
    }

    [Fact]
    public void Attach_MakesStateCurrent()
    {
        var bridge = new NodeEditorStateBridge();
        var state = new NodeEditorState();

        bridge.Attach(state);

        Assert.True(bridge.IsAttached);
        Assert.Same(state, bridge.Current);
    }

    [Fact]
    public void Detach_ClearsCurrentWhenSameInstance()
    {
        var bridge = new NodeEditorStateBridge();
        var state = new NodeEditorState();

        bridge.Attach(state);
        bridge.Detach(state);

        Assert.Null(bridge.Current);
        Assert.False(bridge.IsAttached);
    }

    [Fact]
    public void Detach_DoesNotClearWhenDifferentInstance()
    {
        var bridge = new NodeEditorStateBridge();
        var stateA = new NodeEditorState();
        var stateB = new NodeEditorState();

        bridge.Attach(stateA);
        bridge.Attach(stateB); // newer session takes over

        bridge.Detach(stateA); // old session detaches — should NOT clear

        Assert.True(bridge.IsAttached);
        Assert.Same(stateB, bridge.Current);
    }

    [Fact]
    public void Attach_ThrowsOnNull()
    {
        var bridge = new NodeEditorStateBridge();

        Assert.Throws<ArgumentNullException>(() => bridge.Attach(null!));
    }

    [Fact]
    public void Detach_ThrowsOnNull()
    {
        var bridge = new NodeEditorStateBridge();

        Assert.Throws<ArgumentNullException>(() => bridge.Detach(null!));
    }

    [Fact]
    public void Bridge_IsThreadSafe_ConcurrentAttachDetach()
    {
        var bridge = new NodeEditorStateBridge();
        var states = Enumerable.Range(0, 100).Select(_ => new NodeEditorState()).ToArray();

        Parallel.For(0, 100, i =>
        {
            bridge.Attach(states[i]);
            _ = bridge.Current;
            _ = bridge.IsAttached;
            bridge.Detach(states[i]);
        });

        // Should not throw — thread safety verified
    }
}

public sealed class BridgedNodeEditorStateTests
{
    [Fact]
    public void Throws_WhenNoBridgeAttached()
    {
        var bridge = new NodeEditorStateBridge();
        var bridged = new BridgedNodeEditorState(bridge);

        var ex = Assert.Throws<InvalidOperationException>(() => _ = bridged.Nodes);
        Assert.Contains("No active editor session", ex.Message);
    }

    [Fact]
    public void DelegatesToCurrentState_Nodes()
    {
        var bridge = new NodeEditorStateBridge();
        var state = new NodeEditorState();
        bridge.Attach(state);

        var bridged = new BridgedNodeEditorState(bridge);

        Assert.Same(state.Nodes, bridged.Nodes);
    }

    [Fact]
    public void DelegatesToCurrentState_Connections()
    {
        var bridge = new NodeEditorStateBridge();
        var state = new NodeEditorState();
        bridge.Attach(state);

        var bridged = new BridgedNodeEditorState(bridge);

        Assert.Same(state.Connections, bridged.Connections);
    }

    [Fact]
    public void DelegatesToCurrentState_Variables()
    {
        var bridge = new NodeEditorStateBridge();
        var state = new NodeEditorState();
        bridge.Attach(state);

        var bridged = new BridgedNodeEditorState(bridge);

        Assert.Same(state.Variables, bridged.Variables);
    }

    [Fact]
    public void DelegatesToCurrentState_AddNode()
    {
        var bridge = new NodeEditorStateBridge();
        var state = new NodeEditorState();
        bridge.Attach(state);

        var bridged = new BridgedNodeEditorState(bridge);
        var node = new NodeViewModel(new NodeData(
            Id: "n1", Name: "Test", Callable: false, ExecInit: false,
            Inputs: [], Outputs: []));

        bridged.AddNode(node);

        Assert.Single(state.Nodes);
        Assert.Equal("n1", state.Nodes[0].Data.Id);
    }

    [Fact]
    public void DelegatesToCurrentState_AddConnection()
    {
        var bridge = new NodeEditorStateBridge();
        var state = new NodeEditorState();
        bridge.Attach(state);

        var bridged = new BridgedNodeEditorState(bridge);
        var conn = new ConnectionData("A", "B", "Out", "In", false);

        bridged.AddConnection(conn);

        Assert.Single(state.Connections);
    }

    [Fact]
    public void DelegatesToCurrentState_Clear()
    {
        var bridge = new NodeEditorStateBridge();
        var state = new NodeEditorState();
        bridge.Attach(state);

        // Add nodes first so Clear() -> RemoveNode -> RemoveConnectionsToNode works
        var nodeA = CreateMinimalNode("A");
        var nodeB = CreateMinimalNode("B");
        state.AddNode(nodeA);
        state.AddNode(nodeB);
        state.AddConnection(new ConnectionData("A", "B", "Out", "In", false));

        var bridged = new BridgedNodeEditorState(bridge);
        bridged.Clear();

        Assert.Empty(state.Nodes);
        Assert.Empty(state.Connections);
    }

    [Fact]
    public void FollowsBridgeSwitch()
    {
        var bridge = new NodeEditorStateBridge();
        var stateA = new NodeEditorState();
        var stateB = new NodeEditorState();

        var nodeA = new NodeViewModel(new NodeData(
            Id: "a1", Name: "A", Callable: false, ExecInit: false,
            Inputs: [], Outputs: []));
        stateA.AddNode(nodeA);

        var nodeB = new NodeViewModel(new NodeData(
            Id: "b1", Name: "B", Callable: false, ExecInit: false,
            Inputs: [], Outputs: []));
        stateB.AddNode(nodeB);

        bridge.Attach(stateA);
        var bridged = new BridgedNodeEditorState(bridge);
        Assert.Equal("a1", bridged.Nodes[0].Data.Id);

        bridge.Attach(stateB);
        Assert.Equal("b1", bridged.Nodes[0].Data.Id);
    }

    [Fact]
    public void Throws_AfterDetach()
    {
        var bridge = new NodeEditorStateBridge();
        var state = new NodeEditorState();
        bridge.Attach(state);

        var bridged = new BridgedNodeEditorState(bridge);
        Assert.NotEmpty(bridged.Nodes.Count.ToString()); // access works

        bridge.Detach(state);

        Assert.Throws<InvalidOperationException>(() => _ = bridged.Nodes);
    }

    [Fact]
    public void DelegatesToCurrentState_ZoomProperty()
    {
        var bridge = new NodeEditorStateBridge();
        var state = new NodeEditorState();
        bridge.Attach(state);

        var bridged = new BridgedNodeEditorState(bridge);

        bridged.Zoom = 2.5;
        Assert.Equal(2.5, state.Zoom);
        Assert.Equal(2.5, bridged.Zoom);
    }

    [Fact]
    public void DelegatesToCurrentState_AddVariable()
    {
        var bridge = new NodeEditorStateBridge();
        var state = new NodeEditorState();
        bridge.Attach(state);

        var bridged = new BridgedNodeEditorState(bridge);
        var variable = new GraphVariable("v1", "MyVar", "String", null);

        bridged.AddVariable(variable);

        Assert.Single(state.Variables);
        Assert.Equal("MyVar", state.Variables[0].Name);
    }

    [Fact]
    public void Constructor_ThrowsOnNullBridge()
    {
        Assert.Throws<ArgumentNullException>(() => new BridgedNodeEditorState(null!));
    }

    private static NodeViewModel CreateMinimalNode(string id) =>
        new(new NodeData(id, id, false, false, Array.Empty<SocketData>(), Array.Empty<SocketData>()));
}
