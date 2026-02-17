using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NodeEditor.Net.Models;

namespace NodeEditor.Net.Services.Execution;

internal sealed class NodeExecutionContextImpl : INodeExecutionContext
{
    private readonly ExecutionRuntime _runtime;

    public NodeData Node { get; }
    public IServiceProvider Services => _runtime.GetServicesForNode(Node.Id);
    public CancellationToken CancellationToken => _runtime.CancellationToken;
    public ExecutionEventBus EventBus => _runtime.RuntimeStorage.EventBus;
    public INodeRuntimeStorage RuntimeStorage => _runtime.RuntimeStorage;

    internal NodeExecutionContextImpl(NodeData node, ExecutionRuntime runtime)
    {
        Node = node;
        _runtime = runtime;
    }

    // ── Data I/O ──
    public T GetInput<T>(string socketName)
    {
        // ResolveAllDataInputsAsync runs before ExecuteAsync, so values should be cached.
        if (_runtime.RuntimeStorage.TryGetSocketValue(Node.Id, socketName, out var cached))
            return Cast<T>(cached);

        // Fallback: read the socket default value directly. We intentionally avoid
        // the previous sync-over-async ResolveInputAsync(...).GetAwaiter().GetResult()
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
        => _runtime.RuntimeStorage.SetSocketValue(Node.Id, socketName, value);

    public void SetOutput(string socketName, object? value)
        => _runtime.RuntimeStorage.SetSocketValue(Node.Id, socketName, value);

    // ── Execution flow ──
    public async Task TriggerAsync(string executionOutputName)
    {
        CancellationToken.ThrowIfCancellationRequested();
        await _runtime.Gate.WaitAsync(CancellationToken).ConfigureAwait(false);
        var targets = _runtime.GetExecutionTargets(Node.Id, executionOutputName);

        // If this is a streaming Completed path, ensure all pending OnItem tasks have finished
        // before executing downstream (only relevant if there are downstream connections).
        if (targets.Count > 0)
        {
            await _runtime.WaitForStreamItemsAsync(Node.Id, executionOutputName).ConfigureAwait(false);
        }

        foreach (var (targetNodeId, _) in targets)
        {
            CancellationToken.ThrowIfCancellationRequested();
            await _runtime.ExecuteNodeByIdAsync(targetNodeId).ConfigureAwait(false);
        }
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
            // Fire-and-forget: create an isolated scope so rapid emissions don't
            // overwrite each other's values before downstream reads them.
            var emissionScope = new LayeredRuntimeStorage(_runtime.RuntimeStorage);
            emissionScope.SetSocketValue(Node.Id, streamItemSocket, item);

            var task = Task.Run(async () =>
            {
                try
                {
                    await TriggerScopedAsync(streamInfo.OnItemExecSocket, emissionScope).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
            }, CancellationToken);

            _runtime.RegisterStreamItemTask(Node.Id, streamInfo.CompletedExecSocket, task);
        }
    }

    public Task EmitAsync(string streamItemSocket, object? item)
        => EmitAsync<object?>(streamItemSocket, item);

    // ── Variables ──
    public object? GetVariable(string key) => _runtime.RuntimeStorage.GetVariable(key);
    public void SetVariable(string key, object? value) => _runtime.RuntimeStorage.SetVariable(key, value);

    // ── Feedback ──
    public void EmitFeedback(string message, ExecutionFeedbackType type = ExecutionFeedbackType.DebugPrint, object? tag = null)
        => _runtime.RaiseFeedback(message, Node, type, tag);

    private static T Cast<T>(object? value)
    {
        if (value is T typed) return typed;
        if (value is null) return default!;
        if (value is System.Text.Json.JsonElement json) return System.Text.Json.JsonSerializer.Deserialize<T>(json.GetRawText())!;
        return (T)Convert.ChangeType(value, typeof(T));
    }
}
