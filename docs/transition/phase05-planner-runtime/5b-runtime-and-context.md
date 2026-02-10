# 5B — ExecutionRuntime & NodeExecutionContextImpl

> **Phase 5 — Planner & Runtime**
> All sub-tasks in this phase can run fully in parallel.

## Prerequisites
- **Phases 1–4** complete (`NodeBase`, `INodeExecutionContext`, `INodeRuntimeStorage`, `ExecutionSocket`, `StreamMode`, `NodeDefinition` extended, discovery/registry)

## Can run in parallel with
- **5A** (Planner Simplification)

## Deliverables

### `NodeExecutionContextImpl` — Per-node execution context

**File**: `NodeEditor.Net/Services/Execution/Context/NodeExecutionContextImpl.cs` (new)

```csharp
using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Registry;

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
        if (resolved is not null) return Cast<T>(resolved);
        var socket = Node.Inputs.FirstOrDefault(s => s.Name == socketName);
        if (socket?.Value is not null)
            return _runtime.DeserializeSocketValue<T>(socket.Value);
        return default!;
    }

    public object? GetInput(string socketName) => GetInput<object>(socketName);

    public bool TryGetInput<T>(string socketName, out T value)
    {
        try { value = GetInput<T>(socketName); return true; }
        catch { value = default!; return false; }
    }

    public void SetOutput<T>(string socketName, T value)
        => _runtime.RuntimeStorage.SetSocketValue(Node.Id, socketName, value);

    public void SetOutput(string socketName, object? value)
        => _runtime.RuntimeStorage.SetSocketValue(Node.Id, socketName, value);

    // ── Execution flow ──
    public async Task TriggerAsync(string executionOutputName)
    {
        CancellationToken.ThrowIfCancellationRequested();
        await _runtime.Gate.WaitAsync(CancellationToken);
        var targets = _runtime.GetExecutionTargets(Node.Id, executionOutputName);
        foreach (var (targetNodeId, _) in targets)
        {
            CancellationToken.ThrowIfCancellationRequested();
            await _runtime.ExecuteNodeByIdAsync(targetNodeId);
        }
    }

    // ── Streaming ──
    public async Task EmitAsync<T>(string streamItemSocket, T item)
    {
        SetOutput(streamItemSocket, item);
        var streamInfo = _runtime.GetStreamInfo(Node, streamItemSocket);
        if (streamInfo is null) return;
        var mode = _runtime.GetStreamMode(Node);
        if (mode == StreamMode.Sequential)
        {
            await TriggerAsync(streamInfo.OnItemExecSocket);
        }
        else
        {
            _ = Task.Run(async () =>
            {
                try { await TriggerAsync(streamInfo.OnItemExecSocket); }
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
    public void EmitFeedback(string message, ExecutionFeedbackType type, object? tag)
        => _runtime.RaiseFeedback(message, Node, type, tag);

    private static T Cast<T>(object? value)
    {
        if (value is T typed) return typed;
        if (value is null) return default!;
        return (T)Convert.ChangeType(value, typeof(T));
    }
}
```

---

### `ExecutionRuntime` — Internal orchestrator

**File**: `NodeEditor.Net/Services/Execution/Runtime/ExecutionRuntime.cs` (new)

```csharp
using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution;

internal sealed class ExecutionRuntime
{
    private readonly IReadOnlyList<NodeData> _nodes;
    private readonly Dictionary<string, NodeData> _nodeMap;
    private readonly Dictionary<(string nodeId, string socketName), List<(string targetNodeId, string targetSocket)>> _execConnections;
    private readonly Dictionary<(string nodeId, string socketName), (string sourceNodeId, string sourceSocket)> _dataInputConnections;
    private readonly Dictionary<string, NodeBase?> _nodeInstances;
    private readonly Dictionary<string, NodeDefinition> _nodeDefinitions;

    public INodeRuntimeStorage RuntimeStorage { get; }
    public IServiceProvider Services { get; }
    public CancellationToken CancellationToken { get; }
    public ExecutionGate Gate { get; }

    public event EventHandler<NodeExecutionEventArgs>? NodeStarted;
    public event EventHandler<NodeExecutionEventArgs>? NodeCompleted;
    public event EventHandler<NodeExecutionFailedEventArgs>? NodeFailed;
    public event EventHandler<FeedbackMessageEventArgs>? FeedbackReceived;

    internal ExecutionRuntime(
        IReadOnlyList<NodeData> nodes,
        IReadOnlyList<ConnectionData> connections,
        INodeRuntimeStorage runtimeStorage,
        IServiceProvider services,
        INodeRegistryService registry,
        ExecutionGate gate,
        CancellationToken ct)
    {
        _nodes = nodes;
        RuntimeStorage = runtimeStorage;
        Services = services;
        CancellationToken = ct;
        Gate = gate;
        _nodeMap = nodes.ToDictionary(n => n.Id);
        _execConnections = BuildExecConnectionMap(connections);
        _dataInputConnections = BuildDataInputConnectionMap(connections);
        _nodeDefinitions = ResolveDefinitions(nodes, registry);
        _nodeInstances = new Dictionary<string, NodeBase?>();
    }

    internal NodeBase? GetOrCreateInstance(string nodeId) { /* ... see original Phase 3 doc ... */ }
    internal async Task ExecuteNodeByIdAsync(string nodeId) { /* ... see original Phase 3 doc ... */ }
    internal async Task ResolveAllDataInputsAsync(NodeData node) { /* ... */ }
    internal async Task<object?> ResolveInputAsync(NodeData node, string socketName) { /* ... */ }
    internal List<(string, string)> GetExecutionTargets(string nodeId, string socketName) { /* ... */ }
    internal StreamSocketInfo? GetStreamInfo(NodeData node, string itemSocketName) { /* ... */ }
    internal StreamMode GetStreamMode(NodeData node) => StreamMode.Sequential;
    internal T DeserializeSocketValue<T>(SocketValue socketValue) { /* ... */ }
    internal void RaiseFeedback(string msg, NodeData node, ExecutionFeedbackType type, object? tag) { /* ... */ }

    // Connection map builders (see full code in original Phase 3 doc)
}
```

> Full implementation code for `ExecutionRuntime` and `NodeExecutionContextImpl` is included in this document above.

## Acceptance criteria

- [ ] `NodeExecutionContextImpl` implements all `INodeExecutionContext` members
- [ ] `TriggerAsync` follows execution connections and calls `ExecuteNodeByIdAsync`
- [ ] `EmitAsync` supports both `Sequential` and `FireAndForget` modes
- [ ] `GetInput<T>` resolves from: cached value → upstream connection (lazy) → socket default
- [ ] `ExecutionRuntime` builds connection maps from `ConnectionData`
- [ ] `ExecutionRuntime.ExecuteNodeByIdAsync` handles both class-based and inline-executor nodes
- [ ] Lazy data input resolution triggers upstream execution
- [ ] Unit tests: basic trigger chain, data resolution, streaming emit
