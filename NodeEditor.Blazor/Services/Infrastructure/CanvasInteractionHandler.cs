using Microsoft.AspNetCore.Components.Web;
using System;
using NodeEditor.Blazor.Components;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services.Registry;
using NodeEditor.Blazor.ViewModels;

namespace NodeEditor.Blazor.Services;

/// <summary>
/// Default implementation of <see cref="ICanvasInteractionHandler"/>.
/// Encapsulates pointer, touch, keyboard, and drag-and-drop logic that was
/// previously inlined in <c>NodeEditorCanvas.razor</c>.
/// </summary>
public sealed class CanvasInteractionHandler : ICanvasInteractionHandler
{
    private readonly ICoordinateConverter _coordinateConverter;
    private readonly IConnectionValidator _connectionValidator;
    private readonly ITouchGestureHandler _touchGestures;

    private INodeEditorState _state = null!;

    // ── Panning / zooming ──
    private bool _isPanning;
    private Point2D _panStart;
    private Point2D _panOffset = Point2D.Zero;
    private double _zoom = 1.0;
    private double _touchZoomBase = 1.0;

    // ── Node dragging ──
    private bool _isDraggingNode;
    private Point2D _dragStart;
    private NodeViewModel? _draggingNode;

    // ── Overlay dragging/resizing ──
    private bool _isDraggingOverlay;
    private bool _isResizingOverlay;
    private Point2D _overlayDragStart;
    private OverlayViewModel? _draggingOverlay;
    private Size2D _overlayResizeStartSize;
    private Point2D _overlayResizeStartGraph;
    private List<NodeViewModel> _overlayDragNodes = new();
    private List<OverlayViewModel> _overlayDragOverlays = new();

    // ── Selection ──
    private bool _isSelecting;
    private bool _selectionAdditive;
    private Point2D _selectionStartScreen;
    private Point2D _selectionCurrentScreen;
    private HashSet<string> _selectionBase = new();
    private HashSet<string> _overlaySelectionBase = new();

    // ── Connection drawing ──
    private ConnectionData? _pendingConnection;
    private Point2D? _pendingConnectionEndGraph;

    // ── Context menu ──
    private bool _isContextMenuOpen;
    private Point2D _contextMenuScreenPosition = Point2D.Zero;
    private Point2D _contextMenuGraphPosition = Point2D.Zero;

    // ── Touch ──
    private bool _isTouchGesture;

    // ── Variable drag-and-drop ──
    private VariableDragData? _pendingVariableDrag;

    // ── Clipboard (copy/paste) ──
    private List<GraphNodeData> _clipboardNodes = new();
    private List<ConnectionData> _clipboardConnections = new();
    private int _pasteSequence;

    public CanvasInteractionHandler(
        ICoordinateConverter coordinateConverter,
        IConnectionValidator connectionValidator,
        ITouchGestureHandler touchGestures)
    {
        _coordinateConverter = coordinateConverter;
        _connectionValidator = connectionValidator;
        _touchGestures = touchGestures;
    }

    // ────────────────────────── ICanvasInteractionHandler state ──────────────────────────

    public bool IsPanning => _isPanning;
    public bool IsDraggingNode => _isDraggingNode;
    public bool IsSelecting => _isSelecting;
    public bool IsTouchGesture => _isTouchGesture;
    public bool IsDraggingOverlay => _isDraggingOverlay;
    public bool IsResizingOverlay => _isResizingOverlay;
    public Point2D PanOffset => _panOffset;
    public double Zoom => _zoom;
    public Point2D SelectionStart => _selectionStartScreen;
    public Point2D SelectionCurrent => _selectionCurrentScreen;
    public ConnectionData? PendingConnection => _pendingConnection;
    public Point2D? PendingConnectionEndGraph => _pendingConnectionEndGraph;

    public VariableDragData? PendingVariableDrag
    {
        get => _pendingVariableDrag;
        set => _pendingVariableDrag = value;
    }

    public bool IsContextMenuOpen => _isContextMenuOpen;
    public Point2D ContextMenuScreenPosition => _contextMenuScreenPosition;
    public Point2D ContextMenuGraphPosition => _contextMenuGraphPosition;

    public event Action? StateChanged;

    // ────────────────────────── Lifecycle ──────────────────────────

    public void Attach(INodeEditorState state, double zoom, Point2D panOffset)
    {
        _state = state;
        _zoom = zoom;
        _panOffset = panOffset;
    }

    // ────────────────────────── Pointer events ──────────────────────────

    public void HandlePointerDown(PointerEventArgs e, Point2D canvasPoint)
    {
        if (_isContextMenuOpen && e.Button == 0)
        {
            CloseContextMenu();
        }

        if (e.Button == 1) // Middle mouse button for panning
        {
            _isPanning = true;
            _panStart = new Point2D(canvasPoint.X - _panOffset.X, canvasPoint.Y - _panOffset.Y);
        }
        else if (e.Button == 0) // Left click on empty canvas — start selection
        {
            BeginSelection(e, canvasPoint);
        }
    }

    public void HandlePointerMove(PointerEventArgs e, Point2D canvasPoint)
    {
        if (_isPanning)
        {
            _panOffset = new Point2D(canvasPoint.X - _panStart.X, canvasPoint.Y - _panStart.Y);
            _state.Viewport = new Rect2D(_panOffset.X, _panOffset.Y, _state.Viewport.Width, _state.Viewport.Height);
        }
        else if (_isDraggingOverlay && _draggingOverlay is not null)
        {
            var screenDelta = new Point2D(canvasPoint.X - _overlayDragStart.X, canvasPoint.Y - _overlayDragStart.Y);
            var graphDelta = _coordinateConverter.ScreenDeltaToGraph(screenDelta);

            _draggingOverlay.Position = new Point2D(
                _draggingOverlay.Position.X + graphDelta.X,
                _draggingOverlay.Position.Y + graphDelta.Y);

            foreach (var node in _overlayDragNodes)
            {
                node.Position = new Point2D(
                    node.Position.X + graphDelta.X,
                    node.Position.Y + graphDelta.Y);
            }

            foreach (var overlay in _overlayDragOverlays)
            {
                overlay.Position = new Point2D(
                    overlay.Position.X + graphDelta.X,
                    overlay.Position.Y + graphDelta.Y);
            }

            _overlayDragStart = canvasPoint;
            RaiseStateChanged();
        }
        else if (_isResizingOverlay && _draggingOverlay is not null)
        {
            var graphPoint = _coordinateConverter.ScreenToGraph(canvasPoint);
            var delta = new Point2D(graphPoint.X - _overlayResizeStartGraph.X, graphPoint.Y - _overlayResizeStartGraph.Y);

            var newWidth = Math.Max(120, _overlayResizeStartSize.Width + delta.X);
            var newHeight = Math.Max(80, _overlayResizeStartSize.Height + delta.Y);
            _draggingOverlay.Size = new Size2D(newWidth, newHeight);
            RaiseStateChanged();
        }
        else if (_isDraggingNode && _draggingNode is not null)
        {
            var screenDelta = new Point2D(canvasPoint.X - _dragStart.X, canvasPoint.Y - _dragStart.Y);
            var graphDelta = _coordinateConverter.ScreenDeltaToGraph(screenDelta);

            foreach (var node in GetSelectedNodesForDrag())
            {
                node.Position = new Point2D(
                    node.Position.X + graphDelta.X,
                    node.Position.Y + graphDelta.Y);
            }

            _dragStart = canvasPoint;
            RaiseStateChanged();
        }
        else if (_pendingConnection is not null)
        {
            _pendingConnectionEndGraph = _coordinateConverter.ScreenToGraph(canvasPoint);
            RaiseStateChanged();
        }
        else if (_isSelecting)
        {
            _selectionCurrentScreen = canvasPoint;
            UpdateSelectionFromRect();
        }
    }

    public void HandlePointerUp(PointerEventArgs e, Point2D canvasPoint)
    {
        _isPanning = false;
        _isDraggingNode = false;
        _draggingNode = null;
        _isDraggingOverlay = false;
        _isResizingOverlay = false;
        _draggingOverlay = null;
        _overlayDragNodes = new List<NodeViewModel>();
        _overlayDragOverlays = new List<OverlayViewModel>();
        _pendingConnection = null;
        _pendingConnectionEndGraph = null;

        if (_isSelecting)
        {
            _selectionCurrentScreen = canvasPoint;
            UpdateSelectionFromRect(finalize: true);
            _isSelecting = false;
        }
    }

    public void HandleWheel(WheelEventArgs e, Point2D canvasPoint, double minZoom, double maxZoom, double zoomStep)
    {
        var zoomDelta = e.DeltaY > 0 ? -zoomStep : zoomStep;
        var newZoom = Math.Clamp(_zoom + zoomDelta, minZoom, maxZoom);

        if (Math.Abs(newZoom - _zoom) > double.Epsilon)
        {
            var newPan = _coordinateConverter.ComputeZoomCenteredPan(canvasPoint, _zoom, newZoom);
            _zoom = newZoom;
            _panOffset = newPan;
            _state.Zoom = newZoom;
            _state.Viewport = new Rect2D(newPan.X, newPan.Y, _state.Viewport.Width, _state.Viewport.Height);
        }
    }

    // ────────────────────────── Socket connection events ──────────────────────────

    public void HandleSocketPointerDown(SocketPointerEventArgs e, Point2D canvasPoint)
    {
        if (!e.Socket.Data.IsInput)
        {
            _pendingConnection = new ConnectionData(
                OutputNodeId: e.NodeId,
                InputNodeId: string.Empty,
                OutputSocketName: e.Socket.Data.Name,
                InputSocketName: string.Empty,
                IsExecution: e.Socket.Data.IsExecution);
            _pendingConnectionEndGraph = _coordinateConverter.ScreenToGraph(canvasPoint);
        }
    }

    public void HandleSocketPointerUp(SocketPointerEventArgs e)
    {
        if (_pendingConnection is not null && e.Socket.Data.IsInput)
        {
            var connection = _pendingConnection with
            {
                InputNodeId = e.NodeId,
                InputSocketName = e.Socket.Data.Name
            };

            if (connection.OutputNodeId != connection.InputNodeId
                && IsValidConnection(connection, e.Socket))
            {
                _state.AddConnection(connection);
            }
        }

        _pendingConnection = null;
        _pendingConnectionEndGraph = null;
    }

    // ────────────────────────── Node drag ──────────────────────────

    public void HandleNodeDragStart(NodePointerEventArgs e, Point2D canvasPoint)
    {
        var node = _state.Nodes.FirstOrDefault(n => n.Data.Id == e.NodeId);
        if (node is null) return;

        _isSelecting = false;
        _isDraggingNode = true;
        _draggingNode = node;
        _dragStart = canvasPoint;

        if (!node.IsSelected)
        {
            _state.SelectNode(node.Data.Id, clearExisting: true);
        }
    }

    // ────────────────────────── Overlay drag/resize ──────────────────────────

    public void HandleOverlayPointerDown(OverlayPointerEventArgs e, Point2D canvasPoint)
    {
        var overlay = _state.Overlays.FirstOrDefault(o => o.Id == e.OverlayId);
        if (overlay is null) return;

        _isSelecting = false;
        _isDraggingOverlay = true;
        _isResizingOverlay = false;
        _draggingOverlay = overlay;
        _overlayDragStart = canvasPoint;

        if (e.CtrlKey || e.ShiftKey)
        {
            _state.ToggleSelectOverlay(overlay.Id);
        }
        else
        {
            _state.SelectOverlay(overlay.Id, clearExisting: true);
        }

        var overlayRect = new Rect2D(overlay.Position.X, overlay.Position.Y, overlay.Size.Width, overlay.Size.Height);
        _overlayDragNodes = _state.Nodes
            .Where(n => overlayRect.Intersects(new Rect2D(n.Position.X, n.Position.Y, n.Size.Width, n.Size.Height)))
            .ToList();

        _overlayDragOverlays = _state.Overlays
            .Where(o => o.Id != overlay.Id)
            .Where(o => IsOverlayContained(overlayRect, o))
            .ToList();
    }

    public void HandleOverlayResizeHandleDown(OverlayPointerEventArgs e, Point2D canvasPoint)
    {
        var overlay = _state.Overlays.FirstOrDefault(o => o.Id == e.OverlayId);
        if (overlay is null) return;

        _isSelecting = false;
        _isDraggingOverlay = false;
        _isResizingOverlay = true;
        _draggingOverlay = overlay;
        _overlayResizeStartGraph = _coordinateConverter.ScreenToGraph(canvasPoint);
        _overlayResizeStartSize = overlay.Size;

        if (e.CtrlKey || e.ShiftKey)
        {
            _state.ToggleSelectOverlay(overlay.Id);
        }
        else
        {
            _state.SelectOverlay(overlay.Id, clearExisting: true);
        }
    }

    // ────────────────────────── Touch events ──────────────────────────

    public void HandleTouchStart(IReadOnlyList<TouchPoint2D> touches)
    {
        _isTouchGesture = true;
        var result = _touchGestures.OnTouchStart(touches);
        if (result is null) return;

        if (result.Type == TouchGestureType.DragStart)
        {
            var graphPos = _coordinateConverter.ScreenToGraph(result.Position);
            var nodeAtPosition = FindNodeAtPosition(graphPos);

            if (nodeAtPosition is not null)
            {
                _isDraggingNode = true;
                _draggingNode = nodeAtPosition;
                _dragStart = graphPos;
            }
            else
            {
                _isPanning = true;
                _panStart = result.Position;
            }
        }
    }

    public void HandleTouchMove(IReadOnlyList<TouchPoint2D> touches, double minZoom, double maxZoom)
    {
        if (!_isTouchGesture) return;

        var result = _touchGestures.OnTouchMove(touches);
        if (result is null) return;

        switch (result.Type)
        {
            case TouchGestureType.Pan:
                if (result.Delta.HasValue)
                {
                    _panOffset = new Point2D(
                        _panOffset.X + result.Delta.Value.X,
                        _panOffset.Y + result.Delta.Value.Y);
                    _coordinateConverter.PanOffset = _panOffset;
                    _state.Viewport = new Rect2D(_panOffset.X, _panOffset.Y, _state.Viewport.Width, _state.Viewport.Height);
                }
                break;

            case TouchGestureType.Zoom:
                if (result.ZoomDelta.HasValue && result.ZoomCenter.HasValue)
                {
                    var newZoom = Math.Clamp(_touchZoomBase * result.ZoomDelta.Value, minZoom, maxZoom);
                    var screenCenter = result.ZoomCenter.Value;
                    var graphBefore = _coordinateConverter.ScreenToGraph(screenCenter);

                    _zoom = newZoom;
                    _coordinateConverter.Zoom = _zoom;

                    var graphAfter = _coordinateConverter.ScreenToGraph(screenCenter);
                    var correction = new Point2D(
                        (graphAfter.X - graphBefore.X) * _zoom,
                        (graphAfter.Y - graphBefore.Y) * _zoom);

                    _panOffset = new Point2D(
                        _panOffset.X + correction.X,
                        _panOffset.Y + correction.Y);
                    _coordinateConverter.PanOffset = _panOffset;
                    _state.Zoom = _zoom;
                    _state.Viewport = new Rect2D(_panOffset.X, _panOffset.Y, _state.Viewport.Width, _state.Viewport.Height);
                }
                break;

            case TouchGestureType.Drag:
                if (_isDraggingNode && _draggingNode is not null)
                {
                    var currentPos = _coordinateConverter.ScreenToGraph(result.Position);
                    var delta = new Point2D(
                        currentPos.X - _dragStart.X,
                        currentPos.Y - _dragStart.Y);

                    _draggingNode.Position = new Point2D(
                        _draggingNode.Position.X + delta.X,
                        _draggingNode.Position.Y + delta.Y);

                    _dragStart = currentPos;
                }
                else if (_isPanning)
                {
                    var delta = new Point2D(
                        result.Position.X - _panStart.X,
                        result.Position.Y - _panStart.Y);

                    _panOffset = new Point2D(
                        _panOffset.X + delta.X,
                        _panOffset.Y + delta.Y);
                    _coordinateConverter.PanOffset = _panOffset;
                    _panStart = result.Position;
                    _state.Viewport = new Rect2D(_panOffset.X, _panOffset.Y, _state.Viewport.Width, _state.Viewport.Height);
                }
                break;
        }
    }

    public void HandleTouchEnd(IReadOnlyList<TouchPoint2D> remainingTouches)
    {
        if (!_isTouchGesture) return;

        var result = _touchGestures.OnTouchEnd(remainingTouches);
        if (result is not null)
        {
            switch (result.Type)
            {
                case TouchGestureType.Tap:
                    var graphPos = _coordinateConverter.ScreenToGraph(result.Position);
                    var nodeAtPosition = FindNodeAtPosition(graphPos);

                    if (nodeAtPosition is not null)
                    {
                        _state.SelectNode(nodeAtPosition.Data.Id);
                    }
                    else
                    {
                        _state.ClearConnectionSelection();
                        _state.ClearSelection();
                    }
                    break;

                case TouchGestureType.DragEnd:
                    _isDraggingNode = false;
                    _draggingNode = null;
                    _isPanning = false;
                    _touchZoomBase = _zoom;
                    break;
            }
        }

        if (remainingTouches.Count == 0)
        {
            _isTouchGesture = false;
            _touchGestures.Reset();
        }
    }

    public void HandleTouchCancel()
    {
        _isTouchGesture = false;
        _isDraggingNode = false;
        _draggingNode = null;
        _isPanning = false;
        _touchGestures.Reset();
    }

    // ────────────────────────── Keyboard ──────────────────────────

    public void HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key is "Delete" or "Backspace")
        {
            if (_state.SelectedConnection is not null)
                _state.RemoveConnection(_state.SelectedConnection);
            else if (_state.SelectedOverlayIds.Count > 0)
                _state.RemoveSelectedOverlays();
            else
                _state.RemoveSelectedNodes();
        }
        else if (e.Key == "Escape")
        {
            CancelInteraction();
        }
        else if (e.CtrlKey && (e.Key == "a" || e.Key == "A"))
        {
            _state.SelectAll();
        }
        else if (e.CtrlKey && (e.Key == "c" || e.Key == "C"))
        {
            CopySelectionToClipboard();
        }
        else if (e.CtrlKey && (e.Key == "x" || e.Key == "X"))
        {
            CutSelectionToClipboard();
        }
        else if (e.CtrlKey && (e.Key == "v" || e.Key == "V"))
        {
            PasteClipboard();
        }
        else if (e.CtrlKey && (e.Key == "z" || e.Key == "Z"))
        {
            _state.RequestUndo();
        }
        else if (e.CtrlKey && (e.Key == "y" || e.Key == "Y"))
        {
            _state.RequestRedo();
        }
    }

    // ────────────────────────── Context menu ──────────────────────────

    public void OpenContextMenu(Point2D canvasPoint)
    {
        _contextMenuScreenPosition = canvasPoint;
        _contextMenuGraphPosition = _coordinateConverter.ScreenToGraph(canvasPoint);
        _isContextMenuOpen = true;
    }

    public void CloseContextMenu()
    {
        _isContextMenuOpen = false;
    }

    // ────────────────────────── Variable drag-and-drop ──────────────────────────

    public void HandleDragOver(DragEventArgs e)
    {
        if (_pendingVariableDrag is not null)
        {
            e.DataTransfer.DropEffect = "copy";
        }
    }

    public void HandleDrop(DragEventArgs e, Point2D canvasPoint, INodeRegistryService? registry)
    {
        if (_pendingVariableDrag is null) return;

        var graphPosition = _coordinateConverter.ScreenToGraph(canvasPoint);
        var variableId = _pendingVariableDrag.VariableId;
        var variable = _state.FindVariable(variableId);
        _pendingVariableDrag = null;

        if (variable is null) return;

        var isSetNode = e.AltKey;
        var definitionId = isSetNode ? variable.SetDefinitionId : variable.GetDefinitionId;

        var definition = registry?.Definitions.FirstOrDefault(
            d => d.Id.Equals(definitionId, StringComparison.Ordinal));

        if (definition is null) return;

        var nodeData = definition.Factory();
        var nodeViewModel = new NodeViewModel(nodeData)
        {
            Position = graphPosition
        };

        _state.AddNode(nodeViewModel);
    }

    // ────────────────────────── Clipboard actions ──────────────────────────

    public void CopySelectionToClipboard()
    {
        CopySelectionToClipboardInternal();
    }

    public void CutSelectionToClipboard()
    {
        CopySelectionToClipboardInternal();
        _state.RemoveSelectedNodes();
    }

    public void DeleteSelection()
    {
        if (_state.SelectedConnection is not null)
        {
            _state.RemoveConnection(_state.SelectedConnection);
            return;
        }

        if (_state.SelectedOverlayIds.Count > 0)
        {
            _state.RemoveSelectedOverlays();
            return;
        }

        _state.RemoveSelectedNodes();
    }

    // ────────────────────────── Cancel all interactions ──────────────────────────

    public void CancelInteraction()
    {
        _isPanning = false;
        _isDraggingNode = false;
        _draggingNode = null;
        _isDraggingOverlay = false;
        _isResizingOverlay = false;
        _draggingOverlay = null;
        _overlayDragOverlays = new List<OverlayViewModel>();
        _pendingConnection = null;
        _pendingConnectionEndGraph = null;
        _isSelecting = false;
        CloseContextMenu();
    }

    // ────────────────────────── Internal state used by the canvas for culling ──────────────────────────

    /// <summary>Expose the currently dragged node for culling purposes.</summary>
    internal NodeViewModel? DraggingNode => _draggingNode;

    // ────────────────────────── Private helpers ──────────────────────────

    private void BeginSelection(PointerEventArgs e, Point2D canvasPoint)
    {
        _isSelecting = true;
        _selectionStartScreen = canvasPoint;
        _selectionCurrentScreen = _selectionStartScreen;
        _selectionAdditive = e.CtrlKey || e.ShiftKey;
        _selectionBase = _selectionAdditive ? _state.SelectedNodeIds.ToHashSet() : new HashSet<string>();
        _overlaySelectionBase = _selectionAdditive ? _state.SelectedOverlayIds.ToHashSet() : new HashSet<string>();

        _state.ClearConnectionSelection();

        if (!_selectionAdditive)
        {
            _state.ClearSelection();
        }
    }

    private void UpdateSelectionFromRect(bool finalize = false)
    {
        var selectionRectGraph = GetSelectionRectGraph();
        var selected = _state.Nodes
            .Where(n => selectionRectGraph.Intersects(
                new Rect2D(n.Position.X, n.Position.Y, n.Size.Width, n.Size.Height)))
            .Select(n => n.Data.Id)
            .ToHashSet();

        var selectedOverlays = _state.Overlays
            .Where(o => selectionRectGraph.Intersects(
                new Rect2D(o.Position.X, o.Position.Y, o.Size.Width, o.Size.Height)))
            .Select(o => o.Id)
            .ToHashSet();

        if (_selectionAdditive)
        {
            selected.UnionWith(_selectionBase);
            selectedOverlays.UnionWith(_overlaySelectionBase);
        }

        _state.SelectNodes(selected, clearExisting: true);
        _state.SelectOverlays(selectedOverlays, clearExisting: true);

        if (finalize)
        {
            _selectionBase = selected;
        }
    }

    private Rect2D GetSelectionRectGraph()
    {
        var left = Math.Min(_selectionStartScreen.X, _selectionCurrentScreen.X);
        var top = Math.Min(_selectionStartScreen.Y, _selectionCurrentScreen.Y);
        var width = Math.Abs(_selectionCurrentScreen.X - _selectionStartScreen.X);
        var height = Math.Abs(_selectionCurrentScreen.Y - _selectionStartScreen.Y);

        var screenRect = new Rect2D(left, top, width, height);
        return _coordinateConverter.ScreenToGraph(screenRect);
    }

    private static bool IsOverlayContained(Rect2D container, OverlayViewModel overlay)
    {
        var rect = new Rect2D(overlay.Position.X, overlay.Position.Y, overlay.Size.Width, overlay.Size.Height);
        return container.Contains(rect.Location)
               && container.Contains(new Point2D(rect.X + rect.Width, rect.Y))
               && container.Contains(new Point2D(rect.X, rect.Y + rect.Height))
               && container.Contains(new Point2D(rect.X + rect.Width, rect.Y + rect.Height));
    }

    private IEnumerable<NodeViewModel> GetSelectedNodesForDrag()
    {
        if (_draggingNode is null)
            return Array.Empty<NodeViewModel>();

        return _draggingNode.IsSelected
            ? _state.Nodes.Where(n => n.IsSelected)
            : new[] { _draggingNode };
    }

    private void CopySelectionToClipboardInternal()
    {
        var selectedNodes = _state.Nodes.Where(n => n.IsSelected).ToList();
        if (selectedNodes.Count == 0)
        {
            _clipboardNodes.Clear();
            _clipboardConnections.Clear();
            _pasteSequence = 0;
            return;
        }

        var selectedIds = selectedNodes.Select(n => n.Data.Id).ToHashSet();

        _clipboardNodes = selectedNodes
            .Select(n => new GraphNodeData(n.Data, n.Position, n.Size))
            .ToList();

        _clipboardConnections = _state.Connections
            .Where(c => selectedIds.Contains(c.OutputNodeId) && selectedIds.Contains(c.InputNodeId))
            .ToList();

        _pasteSequence = 0;
    }

    private void PasteClipboard()
    {
        if (_clipboardNodes.Count == 0)
        {
            return;
        }

        var offset = new Point2D(30 * (_pasteSequence + 1), 30 * (_pasteSequence + 1));
        var idMap = new Dictionary<string, string>();
        var newNodeIds = new List<string>();

        foreach (var node in _clipboardNodes)
        {
            var newId = Guid.NewGuid().ToString("N");
            idMap[node.Data.Id] = newId;

            var newData = CloneNodeData(node.Data, newId);
            var newViewModel = new NodeViewModel(newData)
            {
                Position = new Point2D(node.Position.X + offset.X, node.Position.Y + offset.Y),
                Size = node.Size
            };

            _state.AddNode(newViewModel);
            newNodeIds.Add(newViewModel.Data.Id);
        }

        foreach (var connection in _clipboardConnections)
        {
            if (!idMap.TryGetValue(connection.OutputNodeId, out var newOutputId))
                continue;
            if (!idMap.TryGetValue(connection.InputNodeId, out var newInputId))
                continue;

            var newConnection = connection with
            {
                OutputNodeId = newOutputId,
                InputNodeId = newInputId
            };

            _state.AddConnection(newConnection);
        }

        _state.SelectNodes(newNodeIds, clearExisting: true);
        _pasteSequence++;
    }

    private static NodeData CloneNodeData(NodeData data, string newId)
    {
        var inputs = data.Inputs.Select(CloneSocket).ToList();
        var outputs = data.Outputs.Select(CloneSocket).ToList();

        return new NodeData(
            newId,
            data.Name,
            data.Callable,
            data.ExecInit,
            inputs,
            outputs,
            data.DefinitionId);
    }

    private static SocketData CloneSocket(SocketData socket) =>
        new(
            socket.Name,
            socket.TypeName,
            socket.IsInput,
            socket.IsExecution,
            socket.Value,
            socket.EditorHint);

    private bool IsValidConnection(ConnectionData connection, SocketViewModel targetSocket)
    {
        if (_state.Connections.Any(c =>
            c.InputNodeId == connection.InputNodeId &&
            c.InputSocketName == connection.InputSocketName))
        {
            return false;
        }

        var sourceNode = _state.Nodes.FirstOrDefault(n => n.Data.Id == connection.OutputNodeId);
        var sourceSocket = sourceNode?.Outputs.FirstOrDefault(s => s.Data.Name == connection.OutputSocketName);

        if (sourceSocket is null)
            return false;

        return _connectionValidator.CanConnect(sourceSocket.Data, targetSocket.Data);
    }

    private NodeViewModel? FindNodeAtPosition(Point2D graphPosition)
    {
        for (int i = _state.Nodes.Count - 1; i >= 0; i--)
        {
            var node = _state.Nodes[i];
            var nodeRect = new Rect2D(
                node.Position.X,
                node.Position.Y,
                node.Size.Width,
                node.Size.Height);

            if (nodeRect.Contains(graphPosition))
                return node;
        }

        return null;
    }

    private void RaiseStateChanged() => StateChanged?.Invoke();
}
