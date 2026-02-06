namespace NodeEditor.Blazor.Services;

public interface ITouchGestureHandler
{
    TouchGestureResult ProcessTouchStart(TouchPoint[] points);
    TouchGestureResult ProcessTouchMove(TouchPoint[] points);
    TouchGestureResult ProcessTouchEnd(TouchPoint[] points);
    void Reset();
}
