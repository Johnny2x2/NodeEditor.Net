namespace NodeEditor.Net.Services;

/// <summary>
/// Thread-safe singleton implementation of <see cref="INodeEditorStateBridge"/>.
/// </summary>
public sealed class NodeEditorStateBridge : INodeEditorStateBridge
{
    private readonly object _lock = new();
    private INodeEditorState? _current;
    private Func<Func<Task>, Task>? _invokeAsync;

    /// <inheritdoc />
    public INodeEditorState? Current
    {
        get
        {
            lock (_lock)
            {
                return _current;
            }
        }
    }

    /// <inheritdoc />
    public bool IsAttached
    {
        get
        {
            lock (_lock)
            {
                return _current is not null;
            }
        }
    }

    /// <inheritdoc />
    public Func<Func<Task>, Task>? InvokeAsync
    {
        get
        {
            lock (_lock)
            {
                return _invokeAsync;
            }
        }
    }

    /// <inheritdoc />
    public void Attach(INodeEditorState state, Func<Func<Task>, Task>? invokeAsync = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        lock (_lock)
        {
            _current = state;
            _invokeAsync = invokeAsync;
        }
    }

    /// <inheritdoc />
    public void Detach(INodeEditorState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        lock (_lock)
        {
            if (ReferenceEquals(_current, state))
            {
                _current = null;
                _invokeAsync = null;
            }
        }
    }
}
