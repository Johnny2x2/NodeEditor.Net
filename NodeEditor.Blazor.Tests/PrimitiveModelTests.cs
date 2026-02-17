using NodeEditor.Net.Models;

namespace NodeEditor.Blazor.Tests;

public sealed class PrimitiveModelTests
{
    [Fact]
    public void Point2D_Creates_WithExpectedValues()
    {
        var point = new Point2D(3.5, -2);

        Assert.Equal(3.5, point.X);
        Assert.Equal(-2, point.Y);
    }

    [Fact]
    public void Rect2D_FromLocationSize_UsesProvidedValues()
    {
        var rect = Rect2D.FromLocationSize(new Point2D(10, 20), new Size2D(30, 40));

        Assert.Equal(10, rect.X);
        Assert.Equal(20, rect.Y);
        Assert.Equal(30, rect.Width);
        Assert.Equal(40, rect.Height);
    }

    [Fact]
    public void NodeLayout_Default_UsesExpectedDefaults()
    {
        var layout = NodeLayout.Default;

        Assert.Equal(140, layout.Bounds.Width);
        Assert.Equal(60, layout.Bounds.Height);
        Assert.Equal(224, layout.Background.R);
        Assert.Equal(255, layout.Background.G);
        Assert.Equal(255, layout.Background.B);
    }
}
