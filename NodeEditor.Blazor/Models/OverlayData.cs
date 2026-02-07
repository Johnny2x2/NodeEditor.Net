namespace NodeEditor.Blazor.Models;

/// <summary>
/// Data for a background organizer overlay on the canvas.
/// </summary>
public sealed record class OverlayData(
    string Id,
    string Title,
    string Body,
    Point2D Position,
    Size2D Size,
    string Color,
    double Opacity);
