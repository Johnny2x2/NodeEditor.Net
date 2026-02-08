namespace NodeEditor.Blazor.Models;

public readonly record struct Point2D(double X, double Y)
{
    public static Point2D Zero => new(0, 0);
}
