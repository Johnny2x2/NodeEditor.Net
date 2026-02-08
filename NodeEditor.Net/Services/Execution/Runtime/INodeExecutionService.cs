using NodeEditor.Net.Models;

namespace NodeEditor.Net.Services.Execution;

public interface INodeExecutionService
{
    event EventHandler<NodeExecutionEventArgs>? NodeStarted;
    event EventHandler<NodeExecutionEventArgs>? NodeCompleted;
    event EventHandler<NodeExecutionFailedEventArgs>? NodeFailed;
    event EventHandler<Exception>? ExecutionFailed;
    event EventHandler? ExecutionCanceled;
    event EventHandler<ExecutionLayerEventArgs>? LayerStarted;
    event EventHandler<ExecutionLayerEventArgs>? LayerCompleted;

    /// <summary>
    /// Raised when a node emits a feedback message (e.g. from Debug Print nodes).
    /// </summary>
    event EventHandler<FeedbackMessageEventArgs>? FeedbackReceived;

    /// <summary>
    /// Gets the execution gate for step/pause control.
    /// </summary>
    ExecutionGate Gate { get; }

    Task ExecuteAsync(
        IReadOnlyList<NodeData> nodes,
        IReadOnlyList<ConnectionData> connections,
        INodeExecutionContext context,
        object nodeContext,
        NodeExecutionOptions? options,
        CancellationToken token);

    Task ExecutePlannedAsync(
        ExecutionPlan plan,
        IReadOnlyList<ConnectionData> connections,
        INodeExecutionContext context,
        object nodeContext,
        NodeExecutionOptions options,
        CancellationToken token);

    Task ExecuteGroupAsync(
        GroupNodeData group,
        INodeExecutionContext parentContext,
        object nodeContext,
        NodeExecutionOptions? options,
        CancellationToken token);
}
