using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.ViewModels;

/// <summary>
/// View model for organizer overlays (background regions with titles/notes).
/// </summary>
public sealed class OverlayViewModel
{
    public OverlayViewModel(OverlayData data)
    {
        Id = data.Id;
        Title = data.Title;
        Body = data.Body;
        Position = data.Position;
        Size = data.Size;
        Color = data.Color;
        Opacity = data.Opacity;
    }

    public string Id { get; }
    public string Title { get; set; }
    public string Body { get; set; }
    public Point2D Position { get; set; }
    public Size2D Size { get; set; }
    public string Color { get; set; }
    public double Opacity { get; set; }
    public bool IsSelected { get; set; }
}
