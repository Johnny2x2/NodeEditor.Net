namespace NodeEditor.Blazor.Models;

public readonly record struct Rect2D(double X, double Y, double Width, double Height)
{
    public Point2D Location => new(X, Y);
    public Size2D Size => new(Width, Height);

    public static Rect2D FromLocationSize(Point2D location, Size2D size) => new(location.X, location.Y, size.Width, size.Height);

    public bool Intersects(Rect2D other)
    {
        return X <= other.X + other.Width &&
               X + Width >= other.X &&
               Y <= other.Y + other.Height &&
               Y + Height >= other.Y;
    }

    public bool Contains(Point2D point)
    {
        return point.X >= X &&
               point.X <= X + Width &&
               point.Y >= Y &&
               point.Y <= Y + Height;
    }
}
