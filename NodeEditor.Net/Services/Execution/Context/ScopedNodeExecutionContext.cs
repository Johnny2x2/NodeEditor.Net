using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NodeEditor.Net.Models;

namespace NodeEditor.Net.Services.Execution;

/// <summary>
/// A scoped execution context that reads/writes to a <see cref="LayeredRuntimeStorage"/>
/// instead of the shared runtime storage. Used by parallel loop iterations so each iteration's
/// outputs and execution tracking are isolated from other concurrent iterations.
/// </summary>
internal sealed class ScopedNodeExecutionContext : INodeExecutionContext
{
    private readonly ExecutionRuntime _runtime;
    private readonly INodeRuntimeStorage _scope;

    public NodeData Node { get; }
    public IServiceProvider Services => _runtime.GetServicesForNode(Node.Id);
    public CancellationToken CancellationToken => _runtime.CancellationToken;
    public ExecutionEventBus EventBus => _scope.EventBus;
    public INodeRuntimeStorage RuntimeStorage => _scope;

    internal ScopedNodeExecutionContext(NodeData node, ExecutionRuntime runtime, INodeRuntimeStorage scope)
    {
        Node = node;
        _runtime = runtime;
        _scope = scope;
    }

    // ── Data I/O (reads/writes go through the scoped storage) ──

    public T GetInput<T>(string socketName)
    {
        // ResolveAllDataInputsScopedAsync runs before ExecuteAsync, so values should be cached
        // in the scoped storage or read-through from the parent.
        if (_scope.TryGetSocketValue(Node.Id, socketName, out var cached))
            return Cast<T>(cached);

        // Fallback: read the socket default value directly. We intentionally avoid
        // the previous sync-over-async ResolveInputScopedAsync(...).GetAwaiter().GetResult()
        // call which could deadlock on SynchronizationContexts (e.g., Blazor Server).
        var socket = Node.Inputs.FirstOrDefault(s => s.Name == socketName);
        if (socket?.Value is not null)
            return _runtime.DeserializeSocketValue<T>(socket.Value);

        return default!;
    }

    public object? GetInput(string socketName) => GetInput<object>(socketName);

    public bool TryGetInput<T>(string socketName, out T value)
    {
        try
        {
            value = GetInput<T>(socketName);
            return true;
        }
        catch
        {
            value = default!;
            return false;
        }
    }

    public void SetOutput<T>(string socketName, T value)
        => _scope.SetSocketValue(Node.Id, socketName, value);

    public void SetOutput(string socketName, object? value)
        => _scope.SetSocketValue(Node.Id, socketName, value);

    // ── Execution flow ──

    public async Task TriggerAsync(string executionOutputName)
    {
        // Within a scoped context, TriggerAsync routes through scoped execution
        await TriggerScopedAsync(executionOutputName, _scope);
    }

    public async Task TriggerScopedAsync(string executionOutputName, INodeRuntimeStorage scope)
    {
        CancellationToken.ThrowIfCancellationRequested();
        await _runtime.Gate.WaitAsync(CancellationToken).ConfigureAwait(false);
        var targets = _runtime.GetExecutionTargets(Node.Id, executionOutputName);

        if (targets.Count > 0)
        {
            await _runtime.WaitForStreamItemsAsync(Node.Id, executionOutputName).ConfigureAwait(false);
        }

        foreach (var (targetNodeId, _) in targets)
        {
            CancellationToken.ThrowIfCancellationRequested();
            await _runtime.ExecuteNodeByIdScopedAsync(targetNodeId, scope).ConfigureAwait(false);
        }
    }

    // ── Streaming ──

    public async Task EmitAsync<T>(string streamItemSocket, T item)
    {
        SetOutput(streamItemSocket, item);
        var streamInfo = _runtime.GetStreamInfo(Node, streamItemSocket);
        if (streamInfo is null)
            return;

        var mode = _runtime.GetStreamMode(Node);
        if (mode == StreamMode.Sequential)
        {
            await TriggerAsync(streamInfo.OnItemExecSocket).ConfigureAwait(false);
        }
        else
        {
            // Fire-and-forget with per-emission scope for value isolation
            var emissionScope = new LayeredRuntimeStorage(_scope);
            emissionScope.SetSocketValue(Node.Id, streamItemSocket, item);

            var task = Task.Run(async () =>
            {
                try { await TriggerScopedAsync(streamInfo.OnItemExecSocket, emissionScope).ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }, CancellationToken);

            _runtime.RegisterStreamItemTask(Node.Id, streamInfo.CompletedExecSocket, task);
        }
    }

    public Task EmitAsync(string streamItemSocket, object? item)
        => EmitAsync<object?>(streamItemSocket, item);

    // ── Variables ──

    public object? GetVariable(string key) => _scope.GetVariable(key);
    public void SetVariable(string key, object? value) => _scope.SetVariable(key, value);

    // ── Feedback ──

    public void EmitFeedback(string message, ExecutionFeedbackType type = ExecutionFeedbackType.DebugPrint, object? tag = null)
        => _runtime.RaiseFeedback(message, Node, type, tag);

    private static T Cast<T>(object? value)
    {
        if (value is T typed) return typed;
        if (value is null) return default!;
        if (value is System.Text.Json.JsonElement json)
            return System.Text.Json.JsonSerializer.Deserialize<T>(json.GetRawText())!;
        return (T)Convert.ChangeType(value, typeof(T));
    }
}
