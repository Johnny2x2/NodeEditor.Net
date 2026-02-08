namespace NodeEditor.Net.Services;

/// <summary>
/// Singleton bridge that holds a reference to the active <see cref="INodeEditorState"/>.
/// In a single-user desktop app the Blazor circuit attaches its scoped state on init
/// and detaches on dispose, allowing external consumers (e.g. MCP server) to reach the
/// live editor state without holding a scoped dependency.
/// </summary>
public interface INodeEditorStateBridge
{
    /// <summary>The currently attached state, or <c>null</c> if no circuit is active.</summary>
    INodeEditorState? Current { get; }

    /// <summary>Whether a state is currently attached.</summary>
    bool IsAttached { get; }

    /// <summary>
    /// Optional callback that dispatches work onto the Blazor circuit's synchronization
    /// context. Set during <see cref="Attach"/> so that external callers (MCP) can safely
    /// mutate state without triggering "not associated with the Dispatcher" errors.
    /// </summary>
    Func<Func<Task>, Task>? InvokeAsync { get; }

    /// <summary>
    /// Attaches a scoped <see cref="INodeEditorState"/> instance.
    /// Called by the Blazor circuit on initialization.
    /// </summary>
    /// <param name="state">The scoped editor state for this circuit.</param>
    /// <param name="invokeAsync">
    /// Optional dispatcher callback (typically <c>ComponentBase.InvokeAsync</c>)
    /// that marshals work onto the Blazor renderer thread.
    /// </param>
    void Attach(INodeEditorState state, Func<Func<Task>, Task>? invokeAsync = null);

    /// <summary>
    /// Detaches the given state. Only detaches if <paramref name="state"/>
    /// is the currently attached instance (prevents stale circuits from
    /// detaching a newer session).
    /// </summary>
    void Detach(INodeEditorState state);
}
