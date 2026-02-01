using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Services;

/// <summary>
/// Handles touch gesture recognition and processing for mobile/touch devices.
/// Supports single-tap selection, two-finger pan, and pinch zoom gestures.
/// </summary>
public class TouchGestureHandler
{
    private readonly Dictionary<long, TouchPoint> _activeTouches = new();
    private TouchGestureState _gestureState = TouchGestureState.None;
    private double _initialPinchDistance;
    private Point2D _initialPinchCenter = Point2D.Zero;

    /// <summary>
    /// Represents a tracked touch point.
    /// </summary>
    private record TouchPoint(long Identifier, Point2D Position, DateTime StartTime);

    /// <summary>
    /// Types of touch gestures that can be recognized.
    /// </summary>
    private enum TouchGestureState
    {
        None,
        SingleTouch,    // Tap or node drag
        TwoFingerPan,   // Canvas pan
        Pinch           // Zoom
    }

    /// <summary>
    /// The result of processing a touch gesture.
    /// </summary>
    public record TouchGestureResult(
        TouchGestureType Type,
        Point2D Position,
        Point2D? Delta = null,
        double? ZoomDelta = null,
        Point2D? ZoomCenter = null
    );

    /// <summary>
    /// Types of touch gestures.
    /// </summary>
    public enum TouchGestureType
    {
        None,
        Tap,
        Pan,
        Zoom,
        DragStart,
        Drag,
        DragEnd
    }

    /// <summary>
    /// Processes touch start events and determines gesture type.
    /// </summary>
    public TouchGestureResult? OnTouchStart(IReadOnlyList<TouchPoint2D> touches)
    {
        // Clear existing touches and add new ones
        _activeTouches.Clear();
        foreach (var touch in touches)
        {
            _activeTouches[touch.Identifier] = new TouchPoint(
                touch.Identifier,
                new Point2D(touch.ClientX, touch.ClientY),
                DateTime.UtcNow
            );
        }

        // Determine gesture based on touch count
        if (_activeTouches.Count == 1)
        {
            _gestureState = TouchGestureState.SingleTouch;
            var touch = _activeTouches.Values.First();
            return new TouchGestureResult(TouchGestureType.DragStart, touch.Position);
        }
        else if (_activeTouches.Count == 2)
        {
            var touches2 = _activeTouches.Values.ToArray();
            var distance = CalculateDistance(touches2[0].Position, touches2[1].Position);
            var center = CalculateMidpoint(touches2[0].Position, touches2[1].Position);

            _initialPinchDistance = distance;
            _initialPinchCenter = center;
            _gestureState = TouchGestureState.TwoFingerPan;

            return new TouchGestureResult(TouchGestureType.None, center);
        }

        return null;
    }

    /// <summary>
    /// Processes touch move events and generates appropriate gesture results.
    /// </summary>
    public TouchGestureResult? OnTouchMove(IReadOnlyList<TouchPoint2D> touches)
    {
        if (_gestureState == TouchGestureState.None)
            return null;

        // Update active touch positions
        foreach (var touch in touches)
        {
            if (_activeTouches.ContainsKey(touch.Identifier))
            {
                var oldPosition = _activeTouches[touch.Identifier].Position;
                var newPosition = new Point2D(touch.ClientX, touch.ClientY);
                _activeTouches[touch.Identifier] = _activeTouches[touch.Identifier] with { Position = newPosition };
            }
        }

        if (_gestureState == TouchGestureState.SingleTouch && _activeTouches.Count == 1)
        {
            // Single touch drag
            var touch = _activeTouches.Values.First();
            return new TouchGestureResult(TouchGestureType.Drag, touch.Position);
        }
        else if (_activeTouches.Count == 2)
        {
            var touches2 = _activeTouches.Values.ToArray();
            var currentDistance = CalculateDistance(touches2[0].Position, touches2[1].Position);
            var currentCenter = CalculateMidpoint(touches2[0].Position, touches2[1].Position);

            // Check if this is primarily a pinch gesture (distance change > threshold)
            var distanceChange = Math.Abs(currentDistance - _initialPinchDistance);
            var centerDelta = new Point2D(
                currentCenter.X - _initialPinchCenter.X,
                currentCenter.Y - _initialPinchCenter.Y
            );
            var centerMovement = Math.Sqrt(centerDelta.X * centerDelta.X + centerDelta.Y * centerDelta.Y);

            if (distanceChange > 10 && distanceChange > centerMovement * 0.5)
            {
                // Pinch zoom gesture
                _gestureState = TouchGestureState.Pinch;
                var zoomDelta = currentDistance / _initialPinchDistance;
                return new TouchGestureResult(
                    TouchGestureType.Zoom,
                    currentCenter,
                    ZoomDelta: zoomDelta,
                    ZoomCenter: currentCenter
                );
            }
            else if (centerMovement > 5)
            {
                // Two-finger pan gesture
                _gestureState = TouchGestureState.TwoFingerPan;
                return new TouchGestureResult(
                    TouchGestureType.Pan,
                    currentCenter,
                    Delta: centerDelta
                );
            }
        }

        return null;
    }

    /// <summary>
    /// Processes touch end events and finalizes gestures.
    /// </summary>
    public TouchGestureResult? OnTouchEnd(IReadOnlyList<TouchPoint2D> remainingTouches)
    {
        var previousState = _gestureState;
        var previousTouchCount = _activeTouches.Count;

        // Update active touches
        _activeTouches.Clear();
        foreach (var touch in remainingTouches)
        {
            _activeTouches[touch.Identifier] = new TouchPoint(
                touch.Identifier,
                new Point2D(touch.ClientX, touch.ClientY),
                DateTime.UtcNow
            );
        }

        // Handle tap gesture (quick single touch release)
        if (previousState == TouchGestureState.SingleTouch && _activeTouches.Count == 0)
        {
            _gestureState = TouchGestureState.None;
            var lastTouch = _activeTouches.Values.FirstOrDefault();
            if (lastTouch != null)
            {
                var duration = (DateTime.UtcNow - lastTouch.StartTime).TotalMilliseconds;
                if (duration < 300) // Quick tap
                {
                    return new TouchGestureResult(TouchGestureType.Tap, lastTouch.Position);
                }
            }
            return new TouchGestureResult(TouchGestureType.DragEnd, Point2D.Zero);
        }

        // Reset gesture state if all touches released
        if (_activeTouches.Count == 0)
        {
            _gestureState = TouchGestureState.None;
            if (previousTouchCount > 0)
            {
                return new TouchGestureResult(TouchGestureType.DragEnd, Point2D.Zero);
            }
        }
        else if (_activeTouches.Count == 1 && previousTouchCount == 2)
        {
            // Transition from two-finger to single-finger
            _gestureState = TouchGestureState.SingleTouch;
        }

        return null;
    }

    /// <summary>
    /// Resets the gesture handler state.
    /// </summary>
    public void Reset()
    {
        _activeTouches.Clear();
        _gestureState = TouchGestureState.None;
    }

    private static double CalculateDistance(Point2D p1, Point2D p2)
    {
        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static Point2D CalculateMidpoint(Point2D p1, Point2D p2)
    {
        return new Point2D((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
    }
}

/// <summary>
/// Represents a touch point from a touch event.
/// </summary>
public record TouchPoint2D(long Identifier, double ClientX, double ClientY);
