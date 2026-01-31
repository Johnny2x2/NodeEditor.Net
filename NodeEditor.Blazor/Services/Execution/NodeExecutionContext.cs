using System.Collections.Concurrent;

namespace NodeEditor.Blazor.Services.Execution;

public sealed class NodeExecutionContext : INodeExecutionContext
{
    private readonly ConcurrentDictionary<string, object?> _socketValues;
    private readonly ConcurrentDictionary<string, bool> _executedNodes;
    private readonly ConcurrentDictionary<string, object?> _variables;

    public NodeExecutionContext()
    {
        _socketValues = new ConcurrentDictionary<string, object?>(StringComparer.Ordinal);
        _executedNodes = new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);
        _variables = new ConcurrentDictionary<string, object?>(StringComparer.Ordinal);
    }

    private NodeExecutionContext(
        ConcurrentDictionary<string, object?> socketValues,
        ConcurrentDictionary<string, bool> executedNodes,
        ConcurrentDictionary<string, object?> variables)
    {
        _socketValues = socketValues;
        _executedNodes = executedNodes;
        _variables = variables;
    }

    public bool TryGetSocketValue(string nodeId, string socketName, out object? value)
    {
        return _socketValues.TryGetValue(BuildSocketKey(nodeId, socketName), out value);
    }

    public object? GetSocketValue(string nodeId, string socketName)
    {
        _socketValues.TryGetValue(BuildSocketKey(nodeId, socketName), out var value);
        return value;
    }

    public void SetSocketValue(string nodeId, string socketName, object? value)
    {
        _socketValues[BuildSocketKey(nodeId, socketName)] = value;
    }

    public bool IsNodeExecuted(string nodeId)
    {
        return _executedNodes.ContainsKey(nodeId);
    }

    public void MarkNodeExecuted(string nodeId)
    {
        _executedNodes[nodeId] = true;
    }

    public object? GetVariable(string key)
    {
        _variables.TryGetValue(key, out var value);
        return value;
    }

    public void SetVariable(string key, object? value)
    {
        _variables[key] = value;
    }

    public INodeExecutionContext CreateChild(string scopeName, bool inheritVariables = true)
    {
        var socketValues = new ConcurrentDictionary<string, object?>(StringComparer.Ordinal);
        var executedNodes = new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);
        var variables = inheritVariables
            ? new ConcurrentDictionary<string, object?>(_variables, StringComparer.Ordinal)
            : new ConcurrentDictionary<string, object?>(StringComparer.Ordinal);

        return new NodeExecutionContext(socketValues, executedNodes, variables);
    }

    private static string BuildSocketKey(string nodeId, string socketName)
    {
        return string.Concat(nodeId, "::", socketName);
    }
}
