namespace NodeEditor.Blazor.Models;

public readonly record struct ColorValue(byte R, byte G, byte B, byte A = 255)
{
    public static ColorValue Transparent => new(0, 0, 0, 0);
    public static ColorValue FromRgb(byte r, byte g, byte b) => new(r, g, b, 255);
}
