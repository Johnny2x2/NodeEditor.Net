namespace NodeEditor.Net.Services.Execution;

/// <summary>
/// Abstraction for node execution storage and state.
/// </summary>
public interface INodeExecutionContext
{
    bool TryGetSocketValue(string nodeId, string socketName, out object? value);
    object? GetSocketValue(string nodeId, string socketName);
    void SetSocketValue(string nodeId, string socketName, object? value);

    bool IsNodeExecuted(string nodeId);
    void MarkNodeExecuted(string nodeId);
    void ClearNodeExecuted(string nodeId);

    object? GetVariable(string key);
    void SetVariable(string key, object? value);

    // Loop iteration state (scoped to an execution run)
    bool TryGetLoopState<T>(string key, out T value);
    void SetLoopState(string key, object value);
    void ClearLoopState(string key);

    // Iteration generation for loop scoping (prevents stale IsNodeExecuted in loop bodies)
    int CurrentGeneration { get; }
    void PushGeneration();
    void PopGeneration();
    void ClearExecutedForNodes(IEnumerable<string> nodeIds);

    INodeExecutionContext CreateChild(string scopeName, bool inheritVariables = true);

    /// <summary>
    /// Gets the event bus for the current execution run.
    /// Used by Custom Event and Trigger Event nodes.
    /// </summary>
    ExecutionEventBus EventBus { get; }
}
