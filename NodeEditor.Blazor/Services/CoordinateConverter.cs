using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Services;

/// <summary>
/// Provides coordinate conversion utilities between screen space and graph space.
/// Essential for handling zoom, pan, and hit testing in the node editor.
/// </summary>
public sealed class CoordinateConverter
{
    private Point2D _panOffset = Point2D.Zero;
    private double _zoom = 1.0;

    /// <summary>
    /// Gets or sets the current pan offset (translation in screen pixels).
    /// </summary>
    public Point2D PanOffset
    {
        get => _panOffset;
        set => _panOffset = value;
    }

    /// <summary>
    /// Gets or sets the current zoom level (1.0 = 100%).
    /// </summary>
    public double Zoom
    {
        get => _zoom;
        set => _zoom = Math.Max(0.01, value); // Prevent division by zero
    }

    /// <summary>
    /// Converts screen coordinates to graph coordinates.
    /// Use this when handling pointer events to get the graph position.
    /// </summary>
    /// <param name="screenPoint">The point in screen/viewport pixels.</param>
    /// <returns>The point in graph coordinate space.</returns>
    public Point2D ScreenToGraph(Point2D screenPoint)
    {
        return new Point2D(
            (screenPoint.X - _panOffset.X) / _zoom,
            (screenPoint.Y - _panOffset.Y) / _zoom);
    }

    /// <summary>
    /// Converts graph coordinates to screen coordinates.
    /// Use this when positioning elements or computing display locations.
    /// </summary>
    /// <param name="graphPoint">The point in graph coordinate space.</param>
    /// <returns>The point in screen/viewport pixels.</returns>
    public Point2D GraphToScreen(Point2D graphPoint)
    {
        return new Point2D(
            graphPoint.X * _zoom + _panOffset.X,
            graphPoint.Y * _zoom + _panOffset.Y);
    }

    /// <summary>
    /// Converts a screen rectangle to graph coordinates.
    /// </summary>
    public Rect2D ScreenToGraph(Rect2D screenRect)
    {
        var topLeft = ScreenToGraph(new Point2D(screenRect.X, screenRect.Y));
        return new Rect2D(
            topLeft.X,
            topLeft.Y,
            screenRect.Width / _zoom,
            screenRect.Height / _zoom);
    }

    /// <summary>
    /// Converts a graph rectangle to screen coordinates.
    /// </summary>
    public Rect2D GraphToScreen(Rect2D graphRect)
    {
        var topLeft = GraphToScreen(new Point2D(graphRect.X, graphRect.Y));
        return new Rect2D(
            topLeft.X,
            topLeft.Y,
            graphRect.Width * _zoom,
            graphRect.Height * _zoom);
    }

    /// <summary>
    /// Converts a screen delta (movement) to graph delta.
    /// Use this for drag operations where only the difference matters.
    /// </summary>
    public Point2D ScreenDeltaToGraph(Point2D screenDelta)
    {
        return new Point2D(screenDelta.X / _zoom, screenDelta.Y / _zoom);
    }

    /// <summary>
    /// Updates both pan and zoom from state.
    /// Call this when the state changes to keep the converter synchronized.
    /// </summary>
    public void SyncFromState(INodeEditorState state)
    {
        _panOffset = new Point2D(state.Viewport.X, state.Viewport.Y);
        _zoom = state.Zoom;
    }

    /// <summary>
    /// Computes zoom centered on a specific screen point.
    /// Returns the new pan offset that keeps the focus point stationary.
    /// </summary>
    /// <param name="focusScreenPoint">The screen point to zoom towards/from.</param>
    /// <param name="oldZoom">The previous zoom level.</param>
    /// <param name="newZoom">The new zoom level.</param>
    /// <returns>The new pan offset to maintain focus point position.</returns>
    public Point2D ComputeZoomCenteredPan(Point2D focusScreenPoint, double oldZoom, double newZoom)
    {
        // Convert focus point to graph coordinates at old zoom
        var oldConverter = new CoordinateConverter { PanOffset = _panOffset, Zoom = oldZoom };
        var graphPoint = oldConverter.ScreenToGraph(focusScreenPoint);

        // Calculate where this graph point would be at new zoom with current pan
        var newScreenX = graphPoint.X * newZoom + _panOffset.X;
        var newScreenY = graphPoint.Y * newZoom + _panOffset.Y;

        // Adjust pan to keep the focus point at the same screen location
        return new Point2D(
            _panOffset.X + (focusScreenPoint.X - newScreenX),
            _panOffset.Y + (focusScreenPoint.Y - newScreenY));
    }
}
