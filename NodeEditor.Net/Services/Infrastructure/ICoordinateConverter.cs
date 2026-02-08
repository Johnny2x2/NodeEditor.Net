using NodeEditor.Net.Models;

namespace NodeEditor.Net.Services.Infrastructure;

public interface ICoordinateConverter
{
    Point2D PanOffset { get; set; }
    double Zoom { get; set; }

    Point2D ScreenToGraph(Point2D screenPoint);
    Rect2D ScreenToGraph(Rect2D screenRect);
    Point2D GraphToScreen(Point2D graphPoint);
    Rect2D GraphToScreen(Rect2D graphRect);
    Rect2D GetVisibleGraphRect(Size2D viewportSize);
    void ApplyPanDelta(Point2D delta);
    Point2D ScreenDeltaToGraph(Point2D screenDelta);
    Point2D ComputeZoomCenteredPan(Point2D focusScreenPoint, double oldZoom, double newZoom);
    Rect2D GraphRectToScreenRect(Rect2D graphRect);
    void Reset();
    void SyncFromState(INodeEditorState state);
}
