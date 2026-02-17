namespace NodeEditor.Net.Services.Execution;

/// <summary>
/// Base class for all node implementations. Subclass this to create
/// a node with explicit sockets (defined via Configure) and an
/// execution body (ExecuteAsync).
/// </summary>
public abstract class NodeBase
{
    /// <summary>
    /// The unique instance ID of this node on the canvas.
    /// Set by the engine before execution.
    /// </summary>
    public string NodeId { get; internal set; } = string.Empty;

    /// <summary>
    /// Defines the node's metadata and sockets using the builder API.
    /// Called once during discovery/registration — NOT per execution.
    /// </summary>
    public abstract void Configure(INodeBuilder builder);

    /// <summary>
    /// Executes the node's logic. Called by the execution engine.
    /// For callable nodes, this is invoked when the node's execution input is triggered.
    /// For data-only nodes, this is invoked lazily when a downstream node reads an input.
    /// </summary>
    public abstract Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct);

    /// <summary>
    /// Optional lifecycle hook called after the node instance is created,
    /// before ExecuteAsync. Use for DI resolution and one-time setup.
    /// </summary>
    public virtual Task OnCreatedAsync(IServiceProvider services) => Task.CompletedTask;

    /// <summary>
    /// Optional cleanup hook called after execution completes.
    /// </summary>
    public virtual void OnDisposed() { }
}
