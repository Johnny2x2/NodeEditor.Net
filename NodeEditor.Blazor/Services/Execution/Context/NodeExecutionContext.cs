using System.Collections.Concurrent;

namespace NodeEditor.Blazor.Services.Execution;

public sealed class NodeExecutionContext : INodeExecutionContext
{
    private readonly ConcurrentDictionary<string, object?> _socketValues;
    private readonly ConcurrentDictionary<string, bool> _executedNodes;
    private readonly ConcurrentDictionary<string, object?> _variables;
    private readonly ConcurrentDictionary<string, object> _loopState;
    private readonly Stack<int> _generationStack = new();
    private int _currentGeneration;

    public ExecutionEventBus EventBus { get; }

    public NodeExecutionContext()
    {
        _socketValues = new ConcurrentDictionary<string, object?>(StringComparer.Ordinal);
        _executedNodes = new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);
        _variables = new ConcurrentDictionary<string, object?>(StringComparer.Ordinal);
        _loopState = new ConcurrentDictionary<string, object>(StringComparer.Ordinal);
        EventBus = new ExecutionEventBus();
    }

    private NodeExecutionContext(
        ConcurrentDictionary<string, object?> socketValues,
        ConcurrentDictionary<string, bool> executedNodes,
        ConcurrentDictionary<string, object?> variables,
        ConcurrentDictionary<string, object> loopState,
        ExecutionEventBus eventBus)
    {
        _socketValues = socketValues;
        _executedNodes = executedNodes;
        _variables = variables;
        _loopState = loopState;
        EventBus = eventBus;
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

    public void ClearNodeExecuted(string nodeId)
    {
        _executedNodes.TryRemove(nodeId, out _);
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

    // ── Loop state management ──

    public bool TryGetLoopState<T>(string key, out T value)
    {
        if (_loopState.TryGetValue(key, out var stored) && stored is T typed)
        {
            value = typed;
            return true;
        }
        value = default!;
        return false;
    }

    public void SetLoopState(string key, object value)
    {
        _loopState[key] = value;
    }

    public void ClearLoopState(string key)
    {
        _loopState.TryRemove(key, out _);
    }

    // ── Iteration generation (for loop body scoping) ──

    public int CurrentGeneration => _currentGeneration;

    public void PushGeneration()
    {
        _generationStack.Push(_currentGeneration);
        _currentGeneration++;
    }

    public void PopGeneration()
    {
        if (_generationStack.Count > 0)
            _currentGeneration = _generationStack.Pop();
    }

    public void ClearExecutedForNodes(IEnumerable<string> nodeIds)
    {
        foreach (var id in nodeIds)
            _executedNodes.TryRemove(id, out _);
    }

    public INodeExecutionContext CreateChild(string scopeName, bool inheritVariables = true)
    {
        var socketValues = new ConcurrentDictionary<string, object?>(StringComparer.Ordinal);
        var executedNodes = new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);
        var variables = inheritVariables
            ? new ConcurrentDictionary<string, object?>(_variables, StringComparer.Ordinal)
            : new ConcurrentDictionary<string, object?>(StringComparer.Ordinal);
        var loopState = new ConcurrentDictionary<string, object>(StringComparer.Ordinal);

        return new NodeExecutionContext(socketValues, executedNodes, variables, loopState, EventBus);
    }

    private static string BuildSocketKey(string nodeId, string socketName)
    {
        return string.Concat(nodeId, "::", socketName);
    }
}
