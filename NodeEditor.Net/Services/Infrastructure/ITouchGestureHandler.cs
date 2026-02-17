namespace NodeEditor.Net.Services.Infrastructure;

public interface ITouchGestureHandler
{
    TouchGestureResult? OnTouchStart(IReadOnlyList<TouchPoint2D> touches);
    TouchGestureResult? OnTouchMove(IReadOnlyList<TouchPoint2D> touches);
    TouchGestureResult? OnTouchEnd(IReadOnlyList<TouchPoint2D> remainingTouches);
    void Reset();
}
