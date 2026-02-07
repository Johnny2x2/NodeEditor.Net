using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Blazor.Components;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services;
using NodeEditor.Blazor.ViewModels;

namespace NodeEditor.Blazor.Tests;

public sealed class CanvasInteractionHandlerTests
{
    // ─── helpers ───

    private static NodeEditorState CreateState() => new();

    private static CanvasInteractionHandler CreateHandler(
        NodeEditorState? state = null,
        ICoordinateConverter? converter = null,
        IConnectionValidator? validator = null,
        ITouchGestureHandler? touch = null)
    {
        state ??= CreateState();
        converter ??= new CoordinateConverter();
        validator ??= new ConnectionValidator(CreateTypeResolver());
        touch ??= new TouchGestureHandler();

        var handler = new CanvasInteractionHandler(converter, validator, touch);
        handler.Attach(state, 1.0, Point2D.Zero);
        return handler;
    }

    private static ISocketTypeResolver CreateTypeResolver()
    {
        var resolver = new SocketTypeResolver();
        return resolver;
    }

    private static NodeViewModel CreateNode(string id, Point2D? position = null)
    {
        var node = new NodeViewModel(new NodeData(
            id, "Node", false, false,
            new[] { new SocketData("In", "System.String", true, false) },
            new[] { new SocketData("Out", "System.String", false, false) }));
        node.Position = position ?? Point2D.Zero;
        node.Size = new Size2D(100, 60);
        return node;
    }

    private static PointerEventArgs PointerArgs(long button = 0, double clientX = 0, double clientY = 0,
        bool ctrlKey = false, bool shiftKey = false) =>
        new() { Button = button, ClientX = clientX, ClientY = clientY, CtrlKey = ctrlKey, ShiftKey = shiftKey };

    private static WheelEventArgs WheelArgs(double deltaY, double clientX = 0, double clientY = 0) =>
        new() { DeltaY = deltaY, ClientX = clientX, ClientY = clientY };

    private static KeyboardEventArgs KeyArgs(string key, bool ctrlKey = false) =>
        new() { Key = key, CtrlKey = ctrlKey };

    // ════════════════════════════════════════════════════════════════════
    //  LIFECYCLE / ATTACH
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void Attach_SetsZoomAndPanOffset()
    {
        var handler = new CanvasInteractionHandler(
            new CoordinateConverter(), new ConnectionValidator(CreateTypeResolver()), new TouchGestureHandler());

        handler.Attach(CreateState(), 2.5, new Point2D(10, 20));

        Assert.Equal(2.5, handler.Zoom);
        Assert.Equal(new Point2D(10, 20), handler.PanOffset);
    }

    [Fact]
    public void InitialState_IsIdle()
    {
        var handler = CreateHandler();

        Assert.False(handler.IsPanning);
        Assert.False(handler.IsDraggingNode);
        Assert.False(handler.IsSelecting);
        Assert.False(handler.IsTouchGesture);
        Assert.False(handler.IsContextMenuOpen);
        Assert.Null(handler.PendingConnection);
        Assert.Null(handler.PendingConnectionEndGraph);
        Assert.Null(handler.PendingVariableDrag);
    }

    // ════════════════════════════════════════════════════════════════════
    //  PANNING (middle-mouse)
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void MiddleMouseDown_StartsPanning()
    {
        var handler = CreateHandler();

        handler.HandlePointerDown(PointerArgs(button: 1, clientX: 50, clientY: 50), new Point2D(50, 50));

        Assert.True(handler.IsPanning);
    }

    [Fact]
    public void Panning_UpdatesViewport()
    {
        var state = CreateState();
        state.Viewport = new Rect2D(0, 0, 800, 600);
        var handler = CreateHandler(state);

        handler.HandlePointerDown(PointerArgs(button: 1, clientX: 50, clientY: 50), new Point2D(50, 50));
        handler.HandlePointerMove(PointerArgs(button: 1, clientX: 80, clientY: 90), new Point2D(80, 90));

        Assert.NotEqual(Point2D.Zero, handler.PanOffset);
    }

    [Fact]
    public void PointerUp_StopsPanning()
    {
        var handler = CreateHandler();
        handler.HandlePointerDown(PointerArgs(button: 1), new Point2D(50, 50));
        Assert.True(handler.IsPanning);

        handler.HandlePointerUp(PointerArgs(), new Point2D(80, 90));

        Assert.False(handler.IsPanning);
    }

    // ════════════════════════════════════════════════════════════════════
    //  SELECTION
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void LeftClick_BeginsSelection()
    {
        var handler = CreateHandler();

        handler.HandlePointerDown(PointerArgs(button: 0, clientX: 10, clientY: 10), new Point2D(10, 10));

        Assert.True(handler.IsSelecting);
    }

    [Fact]
    public void SelectionRect_UpdatesOnPointerMove()
    {
        var handler = CreateHandler();
        handler.HandlePointerDown(PointerArgs(button: 0), new Point2D(10, 10));

        handler.HandlePointerMove(PointerArgs(), new Point2D(100, 100));

        Assert.Equal(new Point2D(10, 10), handler.SelectionStart);
        Assert.Equal(new Point2D(100, 100), handler.SelectionCurrent);
    }

    [Fact]
    public void PointerUp_FinalizesSelection()
    {
        var handler = CreateHandler();
        handler.HandlePointerDown(PointerArgs(button: 0), new Point2D(10, 10));
        Assert.True(handler.IsSelecting);

        handler.HandlePointerUp(PointerArgs(), new Point2D(100, 100));

        Assert.False(handler.IsSelecting);
    }

    [Fact]
    public void LeftClick_ClearsExistingSelection()
    {
        var state = CreateState();
        var node = CreateNode("n1");
        state.AddNode(node);
        state.SelectNode("n1");
        Assert.True(node.IsSelected);

        var handler = CreateHandler(state);

        handler.HandlePointerDown(PointerArgs(button: 0), new Point2D(500, 500));
        handler.HandlePointerUp(PointerArgs(), new Point2D(500, 500));

        Assert.Empty(state.SelectedNodeIds);
    }

    [Fact]
    public void SelectionRectangle_SelectsNodesInsideRect()
    {
        var state = CreateState();
        var node = CreateNode("n1", new Point2D(50, 50));
        state.AddNode(node);

        var handler = CreateHandler(state);

        // Draw a selection rectangle that encompasses the node
        handler.HandlePointerDown(PointerArgs(button: 0), new Point2D(0, 0));
        handler.HandlePointerMove(PointerArgs(), new Point2D(200, 200));
        handler.HandlePointerUp(PointerArgs(), new Point2D(200, 200));

        Assert.Contains("n1", state.SelectedNodeIds);
    }

    [Fact]
    public void AdditiveSelection_WithCtrl_PreservesExisting()
    {
        var state = CreateState();
        var nodeA = CreateNode("a", new Point2D(50, 50));
        var nodeB = CreateNode("b", new Point2D(500, 500));
        state.AddNode(nodeA);
        state.AddNode(nodeB);
        state.SelectNode("b");

        var handler = CreateHandler(state);

        // Ctrl+drag a rect around nodeA — should keep nodeB selected
        handler.HandlePointerDown(
            PointerArgs(button: 0, ctrlKey: true), new Point2D(0, 0));
        handler.HandlePointerMove(PointerArgs(ctrlKey: true), new Point2D(200, 200));
        handler.HandlePointerUp(PointerArgs(ctrlKey: true), new Point2D(200, 200));

        Assert.Contains("a", state.SelectedNodeIds);
        Assert.Contains("b", state.SelectedNodeIds);
    }

    // ════════════════════════════════════════════════════════════════════
    //  NODE DRAGGING
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void NodeDragStart_BeginsDrag()
    {
        var state = CreateState();
        var node = CreateNode("n1", new Point2D(50, 50));
        state.AddNode(node);

        var handler = CreateHandler(state);
        handler.HandleNodeDragStart(
            new NodePointerEventArgs { NodeId = "n1", Position = new Point2D(70, 70) },
            new Point2D(70, 70));

        Assert.True(handler.IsDraggingNode);
    }

    [Fact]
    public void NodeDragStart_AutoSelectsNode()
    {
        var state = CreateState();
        var node = CreateNode("n1", new Point2D(50, 50));
        state.AddNode(node);

        var handler = CreateHandler(state);
        handler.HandleNodeDragStart(
            new NodePointerEventArgs { NodeId = "n1", Position = new Point2D(70, 70) },
            new Point2D(70, 70));

        Assert.True(node.IsSelected);
        Assert.Contains("n1", state.SelectedNodeIds);
    }

    [Fact]
    public void DraggingNode_MovesPosition()
    {
        var state = CreateState();
        var node = CreateNode("n1", new Point2D(50, 50));
        state.AddNode(node);

        var handler = CreateHandler(state);
        handler.HandleNodeDragStart(
            new NodePointerEventArgs { NodeId = "n1", Position = new Point2D(70, 70) },
            new Point2D(70, 70));

        var originalX = node.Position.X;
        handler.HandlePointerMove(PointerArgs(), new Point2D(80, 80));

        // Position should have shifted
        Assert.NotEqual(originalX, node.Position.X);
    }

    [Fact]
    public void PointerUp_StopsDraggingNode()
    {
        var state = CreateState();
        var node = CreateNode("n1");
        state.AddNode(node);

        var handler = CreateHandler(state);
        handler.HandleNodeDragStart(
            new NodePointerEventArgs { NodeId = "n1", Position = Point2D.Zero },
            Point2D.Zero);
        Assert.True(handler.IsDraggingNode);

        handler.HandlePointerUp(PointerArgs(), Point2D.Zero);

        Assert.False(handler.IsDraggingNode);
    }

    // ════════════════════════════════════════════════════════════════════
    //  CONNECTION DRAWING
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void SocketPointerDown_OnOutput_StartsPendingConnection()
    {
        var handler = CreateHandler();
        var outputSocket = new SocketViewModel(new SocketData("Out", "System.String", false, false));

        handler.HandleSocketPointerDown(
            new SocketPointerEventArgs { NodeId = "n1", Socket = outputSocket, Position = new Point2D(100, 100) },
            new Point2D(100, 100));

        Assert.NotNull(handler.PendingConnection);
        Assert.Equal("n1", handler.PendingConnection.OutputNodeId);
        Assert.Equal("Out", handler.PendingConnection.OutputSocketName);
    }

    [Fact]
    public void SocketPointerDown_OnInput_DoesNotStartConnection()
    {
        var handler = CreateHandler();
        var inputSocket = new SocketViewModel(new SocketData("In", "System.String", true, false));

        handler.HandleSocketPointerDown(
            new SocketPointerEventArgs { NodeId = "n1", Socket = inputSocket, Position = new Point2D(100, 100) },
            new Point2D(100, 100));

        Assert.Null(handler.PendingConnection);
    }

    [Fact]
    public void PendingConnectionEndGraph_UpdatesOnPointerMove()
    {
        var handler = CreateHandler();
        var outputSocket = new SocketViewModel(new SocketData("Out", "System.String", false, false));
        handler.HandleSocketPointerDown(
            new SocketPointerEventArgs { NodeId = "n1", Socket = outputSocket, Position = new Point2D(100, 100) },
            new Point2D(100, 100));

        handler.HandlePointerMove(PointerArgs(), new Point2D(200, 200));

        Assert.NotNull(handler.PendingConnectionEndGraph);
    }

    [Fact]
    public void SocketPointerUp_OnInput_CompletesValidConnection()
    {
        var state = CreateState();
        var nodeA = CreateNode("a");
        var nodeB = CreateNode("b");
        state.AddNode(nodeA);
        state.AddNode(nodeB);

        var handler = CreateHandler(state);

        // Start from output of A
        var outputSocket = nodeA.Outputs.First();
        handler.HandleSocketPointerDown(
            new SocketPointerEventArgs { NodeId = "a", Socket = outputSocket, Position = Point2D.Zero },
            Point2D.Zero);

        // Drop on input of B
        var inputSocket = nodeB.Inputs.First();
        handler.HandleSocketPointerUp(
            new SocketPointerEventArgs { NodeId = "b", Socket = inputSocket, Position = Point2D.Zero });

        Assert.Single(state.Connections);
        Assert.Equal("a", state.Connections[0].OutputNodeId);
        Assert.Equal("b", state.Connections[0].InputNodeId);
    }

    [Fact]
    public void SocketPointerUp_SameNode_DoesNotConnect()
    {
        var state = CreateState();
        var node = CreateNode("n1");
        state.AddNode(node);

        var handler = CreateHandler(state);

        handler.HandleSocketPointerDown(
            new SocketPointerEventArgs { NodeId = "n1", Socket = node.Outputs.First(), Position = Point2D.Zero },
            Point2D.Zero);

        handler.HandleSocketPointerUp(
            new SocketPointerEventArgs { NodeId = "n1", Socket = node.Inputs.First(), Position = Point2D.Zero });

        Assert.Empty(state.Connections);
    }

    [Fact]
    public void SocketPointerUp_DuplicateInput_DoesNotConnect()
    {
        var state = CreateState();
        var nodeA = CreateNode("a");
        var nodeB = CreateNode("b");
        var nodeC = CreateNode("c");
        state.AddNode(nodeA);
        state.AddNode(nodeB);
        state.AddNode(nodeC);

        var handler = CreateHandler(state);

        // First connection A -> B succeeds
        handler.HandleSocketPointerDown(
            new SocketPointerEventArgs { NodeId = "a", Socket = nodeA.Outputs.First(), Position = Point2D.Zero },
            Point2D.Zero);
        handler.HandleSocketPointerUp(
            new SocketPointerEventArgs { NodeId = "b", Socket = nodeB.Inputs.First(), Position = Point2D.Zero });
        Assert.Single(state.Connections);

        // Second connection C -> B (same input) should be rejected
        handler.HandleSocketPointerDown(
            new SocketPointerEventArgs { NodeId = "c", Socket = nodeC.Outputs.First(), Position = Point2D.Zero },
            Point2D.Zero);
        handler.HandleSocketPointerUp(
            new SocketPointerEventArgs { NodeId = "b", Socket = nodeB.Inputs.First(), Position = Point2D.Zero });
        Assert.Single(state.Connections);
    }

    [Fact]
    public void SocketPointerUp_ClearsPendingConnection()
    {
        var state = CreateState();
        var node = CreateNode("n1");
        state.AddNode(node);
        var handler = CreateHandler(state);

        handler.HandleSocketPointerDown(
            new SocketPointerEventArgs { NodeId = "n1", Socket = node.Outputs.First(), Position = Point2D.Zero },
            Point2D.Zero);
        Assert.NotNull(handler.PendingConnection);

        handler.HandleSocketPointerUp(
            new SocketPointerEventArgs { NodeId = "n1", Socket = node.Inputs.First(), Position = Point2D.Zero });
        Assert.Null(handler.PendingConnection);
        Assert.Null(handler.PendingConnectionEndGraph);
    }

    // ════════════════════════════════════════════════════════════════════
    //  KEYBOARD
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void Delete_RemovesSelectedConnection()
    {
        var state = CreateState();
        var connection = new ConnectionData("a", "b", "Out", "In", false);
        state.AddConnection(connection);
        state.SelectConnection(connection);

        var handler = CreateHandler(state);

        handler.HandleKeyDown(KeyArgs("Delete"));

        Assert.Empty(state.Connections);
    }

    [Fact]
    public void Delete_RemovesSelectedNodes_WhenNoConnectionSelected()
    {
        var state = CreateState();
        var node = CreateNode("n1");
        state.AddNode(node);
        state.SelectNode("n1");

        var handler = CreateHandler(state);

        handler.HandleKeyDown(KeyArgs("Delete"));

        Assert.Empty(state.Nodes);
    }

    [Fact]
    public void Escape_CancelsAllInteractions()
    {
        var handler = CreateHandler();

        // Start panning
        handler.HandlePointerDown(PointerArgs(button: 1), new Point2D(50, 50));
        Assert.True(handler.IsPanning);

        handler.HandleKeyDown(KeyArgs("Escape"));

        Assert.False(handler.IsPanning);
        Assert.False(handler.IsDraggingNode);
        Assert.False(handler.IsSelecting);
        Assert.False(handler.IsContextMenuOpen);
        Assert.Null(handler.PendingConnection);
    }

    [Fact]
    public void CtrlA_SelectsAll()
    {
        var state = CreateState();
        state.AddNode(CreateNode("a"));
        state.AddNode(CreateNode("b"));

        var handler = CreateHandler(state);

        handler.HandleKeyDown(KeyArgs("a", ctrlKey: true));

        Assert.Equal(2, state.SelectedNodeIds.Count);
    }

    [Fact]
    public void CtrlZ_RequestsUndo()
    {
        var state = CreateState();
        bool undoRequested = false;
        state.UndoRequested += (_, _) => undoRequested = true;

        var handler = CreateHandler(state);
        handler.HandleKeyDown(KeyArgs("z", ctrlKey: true));

        Assert.True(undoRequested);
    }

    [Fact]
    public void CtrlY_RequestsRedo()
    {
        var state = CreateState();
        bool redoRequested = false;
        state.RedoRequested += (_, _) => redoRequested = true;

        var handler = CreateHandler(state);
        handler.HandleKeyDown(KeyArgs("y", ctrlKey: true));

        Assert.True(redoRequested);
    }

    // ════════════════════════════════════════════════════════════════════
    //  ZOOM (WHEEL)
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void WheelUp_IncreasesZoom()
    {
        var state = CreateState();
        state.Viewport = new Rect2D(0, 0, 800, 600);
        var handler = CreateHandler(state);

        handler.HandleWheel(WheelArgs(deltaY: -100), new Point2D(400, 300), 0.1, 3.0, 0.1);

        Assert.True(state.Zoom > 1.0);
    }

    [Fact]
    public void WheelDown_DecreasesZoom()
    {
        var state = CreateState();
        state.Viewport = new Rect2D(0, 0, 800, 600);
        var handler = CreateHandler(state);

        handler.HandleWheel(WheelArgs(deltaY: 100), new Point2D(400, 300), 0.1, 3.0, 0.1);

        Assert.True(state.Zoom < 1.0);
    }

    [Fact]
    public void Zoom_ClampsToMinMax()
    {
        var state = CreateState();
        state.Viewport = new Rect2D(0, 0, 800, 600);
        var handler = CreateHandler(state);

        // Scroll way down past minimum
        for (int i = 0; i < 50; i++)
            handler.HandleWheel(WheelArgs(deltaY: 100), new Point2D(400, 300), 0.5, 3.0, 0.1);

        Assert.True(state.Zoom >= 0.5);

        // Scroll way up past maximum
        for (int i = 0; i < 100; i++)
            handler.HandleWheel(WheelArgs(deltaY: -100), new Point2D(400, 300), 0.5, 3.0, 0.1);

        Assert.True(state.Zoom <= 3.0);
    }

    // ════════════════════════════════════════════════════════════════════
    //  CONTEXT MENU
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void OpenContextMenu_SetsPositionAndFlag()
    {
        var handler = CreateHandler();

        handler.OpenContextMenu(new Point2D(150, 250));

        Assert.True(handler.IsContextMenuOpen);
        Assert.Equal(new Point2D(150, 250), handler.ContextMenuScreenPosition);
    }

    [Fact]
    public void CloseContextMenu_ClearsFlag()
    {
        var handler = CreateHandler();
        handler.OpenContextMenu(new Point2D(150, 250));

        handler.CloseContextMenu();

        Assert.False(handler.IsContextMenuOpen);
    }

    [Fact]
    public void LeftClick_ClosesOpenContextMenu()
    {
        var handler = CreateHandler();
        handler.OpenContextMenu(new Point2D(150, 250));
        Assert.True(handler.IsContextMenuOpen);

        handler.HandlePointerDown(PointerArgs(button: 0), new Point2D(10, 10));

        Assert.False(handler.IsContextMenuOpen);
    }

    // ════════════════════════════════════════════════════════════════════
    //  CANCEL INTERACTION
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void CancelInteraction_ResetsAllState()
    {
        var state = CreateState();
        state.AddNode(CreateNode("n1"));
        var handler = CreateHandler(state);

        // Start several interactions
        handler.HandlePointerDown(PointerArgs(button: 1), new Point2D(10, 10)); // panning
        handler.OpenContextMenu(new Point2D(50, 50)); // context menu

        handler.CancelInteraction();

        Assert.False(handler.IsPanning);
        Assert.False(handler.IsDraggingNode);
        Assert.False(handler.IsSelecting);
        Assert.False(handler.IsContextMenuOpen);
        Assert.Null(handler.PendingConnection);
        Assert.Null(handler.PendingConnectionEndGraph);
    }

    // ════════════════════════════════════════════════════════════════════
    //  STATE CHANGED EVENT
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void DraggingNode_RaisesStateChanged()
    {
        var state = CreateState();
        var node = CreateNode("n1", new Point2D(50, 50));
        state.AddNode(node);

        var handler = CreateHandler(state);
        int stateChangedCount = 0;
        handler.StateChanged += () => stateChangedCount++;

        handler.HandleNodeDragStart(
            new NodePointerEventArgs { NodeId = "n1", Position = new Point2D(70, 70) },
            new Point2D(70, 70));

        handler.HandlePointerMove(PointerArgs(), new Point2D(100, 100));

        Assert.True(stateChangedCount > 0);
    }

    [Fact]
    public void PendingConnectionMove_RaisesStateChanged()
    {
        var handler = CreateHandler();
        var outputSocket = new SocketViewModel(new SocketData("Out", "System.String", false, false));
        handler.HandleSocketPointerDown(
            new SocketPointerEventArgs { NodeId = "n1", Socket = outputSocket, Position = Point2D.Zero },
            Point2D.Zero);

        int count = 0;
        handler.StateChanged += () => count++;

        handler.HandlePointerMove(PointerArgs(), new Point2D(200, 200));

        Assert.True(count > 0);
    }

    // ════════════════════════════════════════════════════════════════════
    //  TOUCH
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void TouchStart_SetsIsTouchGesture()
    {
        var handler = CreateHandler();

        handler.HandleTouchStart(new[] { new TouchPoint2D(0, 100, 100) });

        Assert.True(handler.IsTouchGesture);
    }

    [Fact]
    public void TouchCancel_ResetsAllTouchState()
    {
        var handler = CreateHandler();
        handler.HandleTouchStart(new[] { new TouchPoint2D(0, 100, 100) });

        handler.HandleTouchCancel();

        Assert.False(handler.IsTouchGesture);
        Assert.False(handler.IsDraggingNode);
        Assert.False(handler.IsPanning);
    }

    [Fact]
    public void TouchEnd_NoRemainingTouches_ResetsGesture()
    {
        var handler = CreateHandler();
        handler.HandleTouchStart(new[] { new TouchPoint2D(0, 100, 100) });

        handler.HandleTouchEnd(Array.Empty<TouchPoint2D>());

        Assert.False(handler.IsTouchGesture);
    }

    // ════════════════════════════════════════════════════════════════════
    //  VARIABLE DRAG-AND-DROP
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void PendingVariableDrag_GetSet_Roundtrips()
    {
        var handler = CreateHandler();
        var data = new VariableDragData("v1", "MyVar");

        handler.PendingVariableDrag = data;

        Assert.Same(data, handler.PendingVariableDrag);
    }

    [Fact]
    public void HandleDrop_WithNoPendingDrag_DoesNothing()
    {
        var state = CreateState();
        var handler = CreateHandler(state);
        var args = new DragEventArgs { DataTransfer = new DataTransfer() };

        handler.HandleDrop(args, Point2D.Zero, null);

        Assert.Empty(state.Nodes);
    }

    // ════════════════════════════════════════════════════════════════════
    //  DI REGISTRATION
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void AddNodeEditor_RegistersCanvasInteractionHandler()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddNodeEditor();

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var handler = scope.ServiceProvider.GetService<ICanvasInteractionHandler>();
        Assert.NotNull(handler);
        Assert.IsType<CanvasInteractionHandler>(handler);
    }
}
