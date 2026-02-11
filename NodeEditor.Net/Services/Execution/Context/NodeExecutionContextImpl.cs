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
    public IServiceProvider Services => _runtime.Services;
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
        if (_runtime.RuntimeStorage.TryGetSocketValue(Node.Id, socketName, out var cached))
            return Cast<T>(cached);

        var resolved = _runtime.ResolveInputAsync(Node, socketName).GetAwaiter().GetResult();
        if (resolved is not null)
            return Cast<T>(resolved);

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
        foreach (var (targetNodeId, _) in targets)
        {
            CancellationToken.ThrowIfCancellationRequested();
            await _runtime.ExecuteNodeByIdAsync(targetNodeId).ConfigureAwait(false);
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
            _ = Task.Run(async () =>
            {
                try { await TriggerAsync(streamInfo.OnItemExecSocket).ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }, CancellationToken);
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
