namespace NodeEditor.Blazor.Models;

public readonly record struct Rect2D(double X, double Y, double Width, double Height)
{
    public Point2D Location => new(X, Y);
    public Size2D Size => new(Width, Height);

    public static Rect2D FromLocationSize(Point2D location, Size2D size) => new(location.X, location.Y, size.Width, size.Height);
}
