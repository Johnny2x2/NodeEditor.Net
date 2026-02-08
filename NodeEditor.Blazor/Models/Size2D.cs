namespace NodeEditor.Blazor.Models;

public readonly record struct Size2D(double Width, double Height)
{
    public static Size2D Empty => new(0, 0);
}
