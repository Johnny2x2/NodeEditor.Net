using NodeEditor.Net.Models;

namespace NodeEditor.Net.Services.Execution;

/// <summary>
/// The execution context passed to NodeBase.ExecuteAsync().
/// Provides high-level APIs for reading inputs, writing outputs,
/// triggering downstream execution paths, and streaming items.
/// </summary>
public interface INodeExecutionContext
{
    /// <summary>The identity of the currently executing node.</summary>
    NodeData Node { get; }

    /// <summary>DI service provider for resolving services during execution.</summary>
    IServiceProvider Services { get; }

    /// <summary>Cancellation token for the current execution run.</summary>
    CancellationToken CancellationToken { get; }

    // ── Data I/O ──

    T GetInput<T>(string socketName);
    object? GetInput(string socketName);
    bool TryGetInput<T>(string socketName, out T value);
    void SetOutput<T>(string socketName, T value);
    void SetOutput(string socketName, object? value);

    // ── Execution flow ──

    /// <summary>
    /// Triggers a named execution output socket. Suspends this node,
    /// executes all connected downstream callable nodes to completion,
    /// then resumes this node.
    /// </summary>
    Task TriggerAsync(string executionOutputName);

    /// <summary>
    /// Triggers a named execution output socket using a scoped storage layer.
    /// Downstream nodes read/write to the provided scope, enabling parallel
    /// iterations to run without interfering with each other's values.
    /// </summary>
    Task TriggerScopedAsync(string executionOutputName, INodeRuntimeStorage scope);

    // ── Streaming ──

    Task EmitAsync<T>(string streamItemSocket, T item);
    Task EmitAsync(string streamItemSocket, object? item);

    // ── Variables ──

    object? GetVariable(string key);
    void SetVariable(string key, object? value);

    // ── Feedback ──

    void EmitFeedback(string message, ExecutionFeedbackType type = ExecutionFeedbackType.DebugPrint,
        object? tag = null);

    // ── Event bus ──

    ExecutionEventBus EventBus { get; }

    // ── Advanced ──

    INodeRuntimeStorage RuntimeStorage { get; }
}
