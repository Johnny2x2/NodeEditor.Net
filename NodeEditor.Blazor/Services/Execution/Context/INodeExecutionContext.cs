namespace NodeEditor.Blazor.Services.Execution;

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

    object? GetVariable(string key);
    void SetVariable(string key, object? value);

    INodeExecutionContext CreateChild(string scopeName, bool inheritVariables = true);
}
