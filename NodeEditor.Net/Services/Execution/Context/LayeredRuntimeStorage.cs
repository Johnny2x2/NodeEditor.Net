using System.Collections.Concurrent;

namespace NodeEditor.Net.Services.Execution;

/// <summary>
/// A layered runtime storage that provides per-scope isolation for parallel loop iterations.
/// 
/// - Socket values: write-local, read-through to parent (upstream data resolved before the loop
///   remains accessible; writes from this iteration don't leak to other iterations or the parent).
/// - Execution tracking: fully local (each iteration independently tracks which nodes it has executed,
///   so loop body nodes appear "not yet executed" in each scope).
/// - Variables: read-through to parent; writes go to the local layer only.
/// - EventBus: shared from parent (events are a global concern).
/// </summary>
public sealed class LayeredRuntimeStorage : INodeRuntimeStorage
{
    private readonly INodeRuntimeStorage _parent;
    private readonly ConcurrentDictionary<string, object?> _localSocketValues = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, bool> _localExecutedNodes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, object?> _localVariables = new(StringComparer.Ordinal);
    private readonly HashSet<string> _localVariableKeys = new(StringComparer.Ordinal);

    public LayeredRuntimeStorage(INodeRuntimeStorage parent)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
    }

    // ── Socket values: write-local, read-through ──

    public bool TryGetSocketValue(string nodeId, string socketName, out object? value)
    {
        var key = BuildSocketKey(nodeId, socketName);
        if (_localSocketValues.TryGetValue(key, out value))
            return true;

        return _parent.TryGetSocketValue(nodeId, socketName, out value);
    }

    public object? GetSocketValue(string nodeId, string socketName)
    {
        var key = BuildSocketKey(nodeId, socketName);
        if (_localSocketValues.TryGetValue(key, out var value))
            return value;

        return _parent.GetSocketValue(nodeId, socketName);
    }

    public void SetSocketValue(string nodeId, string socketName, object? value)
    {
        var key = BuildSocketKey(nodeId, socketName);
        _localSocketValues[key] = value;
    }

    // ── Execution tracking: fully local (no fallback to parent) ──

    public bool IsNodeExecuted(string nodeId)
    {
        return _localExecutedNodes.ContainsKey(nodeId);
    }

    public void MarkNodeExecuted(string nodeId)
    {
        _localExecutedNodes[nodeId] = true;
    }

    public void ClearNodeExecuted(string nodeId)
    {
        _localExecutedNodes.TryRemove(nodeId, out _);
    }

    public void ClearExecutedForNodes(IEnumerable<string> nodeIds)
    {
        foreach (var id in nodeIds)
            _localExecutedNodes.TryRemove(id, out _);
    }

    // ── Variables: read-through to parent, write-local ──

    public object? GetVariable(string key)
    {
        if (_localVariableKeys.Contains(key))
        {
            _localVariables.TryGetValue(key, out var value);
            return value;
        }

        return _parent.GetVariable(key);
    }

    public void SetVariable(string key, object? value)
    {
        _localVariables[key] = value;
        _localVariableKeys.Add(key);
    }

    // ── Generation: delegate to parent ──

    public int CurrentGeneration => _parent.CurrentGeneration;

    public void PushGeneration() => _parent.PushGeneration();

    public void PopGeneration() => _parent.PopGeneration();

    // ── EventBus: shared from parent ──

    public ExecutionEventBus EventBus => _parent.EventBus;

    // ── Child creation ──

    public INodeRuntimeStorage CreateChild(string scopeName, bool inheritVariables = true)
    {
        // Create a further nested layer on top of this one.
        return new LayeredRuntimeStorage(this);
    }

    private static string BuildSocketKey(string nodeId, string socketName)
    {
        return string.Concat(nodeId, "::", socketName);
    }
}
