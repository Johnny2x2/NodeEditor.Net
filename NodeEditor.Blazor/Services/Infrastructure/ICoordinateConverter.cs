using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Services;

public interface ICoordinateConverter
{
    Point2D PanOffset { get; set; }
    double Zoom { get; set; }

    Point2D ScreenToGraph(Point2D screenPoint);
    Point2D GraphToScreen(Point2D graphPoint);
    Rect2D GetVisibleGraphRect(Size2D viewportSize);
    void ApplyPanDelta(Point2D delta);
    Rect2D GraphRectToScreenRect(Rect2D graphRect);
    void Reset();
    void SyncFromState(INodeEditorState state);
}
