namespace NodeEditor.Net.Services.Execution;

/// <summary>
/// Low-level runtime storage for node execution state.
/// Used internally by the execution engine. Most node implementations
/// should use INodeExecutionContext's high-level APIs instead.
/// </summary>
public interface INodeRuntimeStorage
{
    bool TryGetSocketValue(string nodeId, string socketName, out object? value);
    object? GetSocketValue(string nodeId, string socketName);
    void SetSocketValue(string nodeId, string socketName, object? value);

    bool IsNodeExecuted(string nodeId);
    void MarkNodeExecuted(string nodeId);
    void ClearNodeExecuted(string nodeId);

    object? GetVariable(string key);
    void SetVariable(string key, object? value);

    int CurrentGeneration { get; }
    void PushGeneration();
    void PopGeneration();
    void ClearExecutedForNodes(IEnumerable<string> nodeIds);

    INodeRuntimeStorage CreateChild(string scopeName, bool inheritVariables = true);

    ExecutionEventBus EventBus { get; }
}
