using Microsoft.AspNetCore.Components.Web;
using NodeEditor.Blazor.Components;
using NodeEditor.Net.Models;
using NodeEditor.Net.Services;
using NodeEditor.Net.Services.Infrastructure;
using NodeEditor.Net.Services.Registry;
using NodeEditor.Net.ViewModels;

namespace NodeEditor.Blazor.Services;

/// <summary>
/// Abstraction for canvas pointer/touch/keyboard interaction logic.
/// Extracted from <c>NodeEditorCanvas</c> to keep the Razor component
/// a thin coordinator while the interaction state machine lives in a
/// testable, swappable service.
/// </summary>
public interface ICanvasInteractionHandler
{
    // ── Current interaction state (read by the canvas for rendering) ──

    bool IsPanning { get; }
    bool IsDraggingNode { get; }
    bool IsSelecting { get; }
    bool IsTouchGesture { get; }
    bool IsDraggingOverlay { get; }
    bool IsResizingOverlay { get; }

    Point2D PanOffset { get; }
    double Zoom { get; }

    /// <summary>Selection rectangle corners in canvas-screen space.</summary>
    Point2D SelectionStart { get; }
    Point2D SelectionCurrent { get; }

    /// <summary>The in-progress connection being drawn from an output socket, or null.</summary>
    ConnectionData? PendingConnection { get; }

    /// <summary>The current end-point (in graph space) of the connection being drawn.</summary>
    Point2D? PendingConnectionEndGraph { get; }

    /// <summary>Variable drag-and-drop payload set by the variables panel.</summary>
    VariableDragData? PendingVariableDrag { get; set; }

    // ── Lifecycle ──

    /// <summary>Initialise the handler with the current editor state and zoom/pan values.</summary>
    void Attach(INodeEditorState state, double zoom, Point2D panOffset);

    // ── Pointer events ──

    void HandlePointerDown(PointerEventArgs e, Point2D canvasPoint);
    void HandlePointerMove(PointerEventArgs e, Point2D canvasPoint);
    void HandlePointerUp(PointerEventArgs e, Point2D canvasPoint);
    void HandleWheel(WheelEventArgs e, Point2D canvasPoint, double minZoom, double maxZoom, double zoomStep);

    // ── Socket connection events ──

    void HandleSocketPointerDown(SocketPointerEventArgs e, Point2D canvasPoint);
    void HandleSocketPointerUp(SocketPointerEventArgs e);

    // ── Node drag ──

    void HandleNodeDragStart(NodePointerEventArgs e, Point2D canvasPoint);

    // ── Overlay drag/resize ──

    void HandleOverlayPointerDown(OverlayPointerEventArgs e, Point2D canvasPoint);
    void HandleOverlayResizeHandleDown(OverlayPointerEventArgs e, Point2D canvasPoint);

    // ── Touch events ──

    void HandleTouchStart(IReadOnlyList<TouchPoint2D> touches);
    void HandleTouchMove(IReadOnlyList<TouchPoint2D> touches, double minZoom, double maxZoom);
    void HandleTouchEnd(IReadOnlyList<TouchPoint2D> remainingTouches);
    void HandleTouchCancel();

    // ── Keyboard ──

    void HandleKeyDown(KeyboardEventArgs e);

    // ── Context menu ──

    bool IsContextMenuOpen { get; }
    Point2D ContextMenuScreenPosition { get; }
    Point2D ContextMenuGraphPosition { get; }
    void OpenContextMenu(Point2D canvasPoint);
    void CloseContextMenu();

    // ── Variable drag-and-drop ──

    void HandleDragOver(DragEventArgs e);
    void HandleDrop(DragEventArgs e, Point2D canvasPoint, INodeRegistryService? registry);

    // ── Clipboard ──

    void CopySelectionToClipboard();
    void CutSelectionToClipboard();
    void DeleteSelection();

    // ── Utility ──

    void CancelInteraction();

    /// <summary>Raised whenever the UI should re-render (state changed).</summary>
    event Action? StateChanged;
}
