using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Tests;

public sealed class Rect2DTests
{
    [Fact]
    public void Intersects_WhenOverlapping_ReturnsTrue()
    {
        var a = new Rect2D(0, 0, 100, 100);
        var b = new Rect2D(50, 50, 100, 100);

        Assert.True(a.Intersects(b));
    }

    [Fact]
    public void Intersects_WhenTouchingEdges_ReturnsTrue()
    {
        var a = new Rect2D(0, 0, 100, 100);
        var b = new Rect2D(100, 0, 50, 50);

        Assert.True(a.Intersects(b));
    }

    [Fact]
    public void Intersects_WhenSeparate_ReturnsFalse()
    {
        var a = new Rect2D(0, 0, 100, 100);
        var b = new Rect2D(200, 200, 50, 50);

        Assert.False(a.Intersects(b));
    }

    [Fact]
    public void Contains_WhenInside_ReturnsTrue()
    {
        var rect = new Rect2D(0, 0, 100, 100);
        var point = new Point2D(10, 10);

        Assert.True(rect.Contains(point));
    }

    [Fact]
    public void Contains_WhenOutside_ReturnsFalse()
    {
        var rect = new Rect2D(0, 0, 100, 100);
        var point = new Point2D(200, 200);

        Assert.False(rect.Contains(point));
    }
}