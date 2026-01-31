# Event-Based Architecture Guide

## Overview

The NodeEditorMax Blazor implementation uses an event-based architecture for state management. This approach provides optimal performance for Blazor rendering and enables reactive UI updates.

## Architecture Pattern

### Event-Based vs Immutable Snapshots

We chose an **event-based approach** over immutable snapshots for the following reasons:

1. **Performance**: Events allow Blazor components to subscribe only to relevant changes, avoiding unnecessary re-renders
2. **Memory efficiency**: No need to create full state copies on every change
3. **Blazor best practices**: Aligns with .NET's event pattern and INotifyPropertyChanged
4. **Real-time updates**: Provides immediate notifications to all subscribers
5. **History tracking**: Events can be logged for undo/redo functionality

## Core Components

### NodeEditorState

The `NodeEditorState` class is the central state management service. It:
- Maintains collections of nodes and connections
- Tracks selection state and viewport
- Raises events when state changes occur
- Follows the Observer pattern

### Event Types

| Event | When Raised | Event Args |
|-------|------------|------------|
| `NodeAdded` | When a node is added to the graph | `NodeEventArgs` |
| `NodeRemoved` | When a node is removed from the graph | `NodeEventArgs` |
| `ConnectionAdded` | When a connection is created | `ConnectionEventArgs` |
| `ConnectionRemoved` | When a connection is deleted | `ConnectionEventArgs` |
| `SelectionChanged` | When node selection changes | `SelectionChangedEventArgs` |
| `ViewportChanged` | When the visible area changes | `ViewportChangedEventArgs` |
| `ZoomChanged` | When the zoom level changes | `ZoomChangedEventArgs` |

## Usage in Blazor Components

### Basic Subscription

```csharp
@implements IDisposable
@inject NodeEditorState EditorState

@code {
    protected override void OnInitialized()
    {
        // Subscribe to relevant events
        EditorState.NodeAdded += OnNodeAdded;
        EditorState.SelectionChanged += OnSelectionChanged;
    }

    private void OnNodeAdded(object? sender, NodeEventArgs e)
    {
        // Handle the event
        Console.WriteLine($"Node added: {e.Node.Data.Name}");
        
        // Trigger Blazor re-render if needed
        StateHasChanged();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Access previous and current selection
        var added = e.CurrentSelection.Except(e.PreviousSelection);
        var removed = e.PreviousSelection.Except(e.CurrentSelection);
        
        StateHasChanged();
    }

    public void Dispose()
    {
        // Always unsubscribe to prevent memory leaks
        EditorState.NodeAdded -= OnNodeAdded;
        EditorState.SelectionChanged -= OnSelectionChanged;
    }
}
```

### Selective Re-rendering

Only subscribe to events that affect your component:

```csharp
// A connection list component only needs connection events
public class ConnectionListComponent : ComponentBase, IDisposable
{
    [Inject] NodeEditorState EditorState { get; set; }

    protected override void OnInitialized()
    {
        // Only subscribe to connection-related events
        EditorState.ConnectionAdded += OnConnectionChanged;
        EditorState.ConnectionRemoved += OnConnectionChanged;
        // No need to subscribe to NodeAdded, SelectionChanged, etc.
    }

    private void OnConnectionChanged(object? sender, EventArgs e)
    {
        StateHasChanged();
    }

    public void Dispose()
    {
        EditorState.ConnectionAdded -= OnConnectionChanged;
        EditorState.ConnectionRemoved -= OnConnectionChanged;
    }
}
```

### Modifying State

Always use the provided methods instead of directly modifying collections:

```csharp
// ✅ Correct - raises events
EditorState.AddNode(newNode);
EditorState.RemoveNode("nodeId");
EditorState.SelectNode("nodeId");

// ❌ Incorrect - does not raise events
EditorState.Nodes.Add(newNode); // Don't do this!
EditorState.Nodes.Remove(node); // Don't do this!
```

## Best Practices

### 1. Always Dispose Subscriptions

```csharp
@implements IDisposable

public void Dispose()
{
    EditorState.NodeAdded -= OnNodeAdded;
    // Unsubscribe from all events
}
```

### 2. Use Specific Event Handlers

Create separate handlers for different concerns:

```csharp
protected override void OnInitialized()
{
    EditorState.SelectionChanged += OnSelectionChanged;
    EditorState.ZoomChanged += OnZoomChanged;
}

private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
{
    // Handle selection logic
}

private void OnZoomChanged(object? sender, ZoomChangedEventArgs e)
{
    // Handle zoom logic
}
```

### 3. Minimize Re-renders

Only call `StateHasChanged()` when necessary:

```csharp
private void OnNodeAdded(object? sender, NodeEventArgs e)
{
    // Only re-render if this component displays nodes
    if (ShouldDisplayNode(e.Node))
    {
        StateHasChanged();
    }
}
```

### 4. Thread Safety

Event handlers are invoked synchronously on the same thread as the state modification. For async operations:

```csharp
private async void OnNodeAdded(object? sender, NodeEventArgs e)
{
    // Use InvokeAsync for async operations that affect UI
    await InvokeAsync(async () =>
    {
        await SomeAsyncOperation();
        StateHasChanged();
    });
}
```

## Performance Considerations

### ObservableCollection vs Events

- `ObservableCollection` provides `CollectionChanged` events for fine-grained collection modifications
- Custom events on `NodeEditorState` provide semantic meaning (e.g., "selection changed")
- Use both together: `ObservableCollection` for data binding, custom events for component logic

### Event Granularity

Events are raised for every state change. For batch operations, consider:

```csharp
// Option 1: Temporarily disable events (if implemented)
// Option 2: Batch operations and raise a single event
// Option 3: Use InvokeAsync to batch UI updates
```

## Testing

The event system is fully tested. See `StateEventTests.cs` for examples:

```csharp
[Fact]
public void AddNode_RaisesNodeAddedEvent()
{
    var state = new NodeEditorState();
    NodeEventArgs? raisedArgs = null;
    state.NodeAdded += (sender, args) => raisedArgs = args;

    state.AddNode(node);

    Assert.NotNull(raisedArgs);
    Assert.Equal(node, raisedArgs.Node);
}
```

## Migration from Direct Collection Access

If you have existing code that directly modifies collections:

```csharp
// Before
state.Nodes.Add(newNode);

// After
state.AddNode(newNode);
```

Update all direct collection modifications to use the new methods.

## Future Enhancements

The event-based architecture supports:
- **Undo/Redo**: Record events for history tracking
- **Remote collaboration**: Broadcast events to other clients
- **Change detection**: Track dirty state for save prompts
- **Performance monitoring**: Log event frequency for optimization
