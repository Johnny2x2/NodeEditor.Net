namespace NodeEditor.Blazor.Models;

public sealed record class StrokeStyle(double Thickness, ColorValue Color, bool IsDashed = false)
{
    public static StrokeStyle Default { get; } = new(1, new ColorValue(0, 0, 0));
}
