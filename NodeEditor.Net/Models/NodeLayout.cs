namespace NodeEditor.Net.Models;

public sealed record class NodeLayout(Rect2D Bounds, ColorValue Background, StrokeStyle Border)
{
    public static NodeLayout Default => new(new Rect2D(0, 0, 140, 60), new ColorValue(224, 255, 255), StrokeStyle.Default);
}
