# Phase 3 — Execution Engine Rewrite

> **Goal**: Replace the plan-driven execution model (topological sort → step dispatch → re-invoke methods with `ExecutionPath.Signal()`) with a **coroutine-driven model** where nodes own their control flow via `TriggerAsync()`.

## Architecture shift

| Aspect | Old (plan-driven) | New (coroutine-driven) |
|--------|-------------------|----------------------|
| **Entry** | `ExecuteAsync()` → `BuildHierarchicalPlan()` → `ExecuteStepsAsync()` | `ExecuteAsync()` → find initiator nodes → `nodeInstance.ExecuteAsync(ctx, ct)` |
| **Loops** | `LoopStep` detected by planner; engine re-invokes header method each iteration, checks `ExecutionPath.IsSignaled`, clears body node execution flags | Node's `ExecuteAsync()` contains a real `for`/`while` loop; calls `ctx.TriggerAsync("LoopPath")` per iteration |
| **Branches** | Engine executes all layer nodes; downstream nodes check if upstream exec-path was signaled; skip if not | Node calls `ctx.TriggerAsync("True")` or `ctx.TriggerAsync("False")` — only the chosen path runs |
| **Data resolution** | `ResolveInputsAsync()` DFS: walks upstream data connections, lazily executes non-callable nodes | Same — `ctx.GetInput<T>()` internally walks upstream data connections, lazily executes data-only nodes |
| **Node dispatch** | 3-way: `VariableNodeExecutor` / `EventNodeExecutor` / `NodeMethodInvoker` (reflection) | 2-way: `NodeBase.ExecuteAsync()` (or `InlineExecutor` delegate) / special handling for variable/event nodes |
| **Step debugging** | `ExecutionGate` checks before each step | `ExecutionGate` checks before each `TriggerAsync()` dispatch |

---

## 3.1 New `NodeExecutionContextImpl`

**File**: `NodeEditor.Net/Services/Execution/Context/NodeExecutionContextImpl.cs` (new)

This is the heart of the new system — it implements `INodeExecutionContext` and orchestrates the coroutine-based execution.

```csharp
using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution;

/// <summary>
/// Implements INodeExecutionContext for a single node during execution.
/// Each canvas node gets its own instance.
/// </summary>
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
        // 1. Check if value already resolved in runtime storage
        if (_runtime.RuntimeStorage.TryGetSocketValue(Node.Id, socketName, out var cached))
            return Cast<T>(cached);

        // 2. Check wired upstream connections — resolve lazily
        var resolved = _runtime.ResolveInputAsync(Node, socketName).GetAwaiter().GetResult();
        if (resolved is not null)
            return Cast<T>(resolved);

        // 3. Fall back to socket default value
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
    {
        _runtime.RuntimeStorage.SetSocketValue(Node.Id, socketName, value);
    }

    public void SetOutput(string socketName, object? value)
    {
        _runtime.RuntimeStorage.SetSocketValue(Node.Id, socketName, value);
    }

    // ── Execution flow ──

    public async Task TriggerAsync(string executionOutputName)
    {
        CancellationToken.ThrowIfCancellationRequested();

        // Check execution gate (step-debug support)
        await _runtime.Gate.WaitAsync(CancellationToken);

        // Find all connections from this node's execution output
        var targets = _runtime.GetExecutionTargets(Node.Id, executionOutputName);

        foreach (var (targetNodeId, targetSocketName) in targets)
        {
            CancellationToken.ThrowIfCancellationRequested();
            await _runtime.ExecuteNodeByIdAsync(targetNodeId);
        }
    }

    // ── Streaming ──

    public async Task EmitAsync<T>(string streamItemSocket, T item)
    {
        // Set the item value on the data output socket
        SetOutput(streamItemSocket, item);

        // Find the associated per-item execution socket
        var streamInfo = _runtime.GetStreamInfo(Node, streamItemSocket);
        if (streamInfo is null) return;

        var mode = _runtime.GetStreamMode(Node);

        if (mode == StreamMode.Sequential)
        {
            await TriggerAsync(streamInfo.OnItemExecSocket);
        }
        else // FireAndForget
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
    {
        _runtime.RaiseFeedback(message, Node, type, tag);
    }

    private static T Cast<T>(object? value)
    {
        if (value is T typed) return typed;
        if (value is null) return default!;
        return (T)Convert.ChangeType(value, typeof(T));
    }
}
```

---

## 3.2 `ExecutionRuntime` — Internal orchestrator

**File**: `NodeEditor.Net/Services/Execution/Runtime/ExecutionRuntime.cs` (new)

This is the internal class that `NodeExecutionContextImpl` delegates to. It holds the full graph state for a single execution run.

```csharp
using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution;

/// <summary>
/// Internal orchestrator for a single execution run.
/// Holds the graph topology, node instances, and runtime state.
/// Created by NodeExecutionService for each execution.
/// </summary>
internal sealed class ExecutionRuntime
{
    // ── Graph topology ──
    private readonly IReadOnlyList<NodeData> _nodes;
    private readonly Dictionary<string, NodeData> _nodeMap;                          // nodeId → NodeData
    private readonly Dictionary<(string nodeId, string socketName), List<(string targetNodeId, string targetSocket)>> _execConnections;
    private readonly Dictionary<(string nodeId, string socketName), (string sourceNodeId, string sourceSocket)> _dataInputConnections;

    // ── Node instances ──
    private readonly Dictionary<string, NodeBase?> _nodeInstances;                   // nodeId → NodeBase instance
    private readonly Dictionary<string, NodeDefinition> _nodeDefinitions;            // nodeId → NodeDefinition

    // ── Runtime state ──
    public INodeRuntimeStorage RuntimeStorage { get; }
    public IServiceProvider Services { get; }
    public CancellationToken CancellationToken { get; }
    public ExecutionGate Gate { get; }

    // ── Events ──
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

        // Build lookup maps
        _nodeMap = nodes.ToDictionary(n => n.Id);
        _execConnections = BuildExecConnectionMap(connections);
        _dataInputConnections = BuildDataInputConnectionMap(connections);
        _nodeDefinitions = ResolveDefinitions(nodes, registry);
        _nodeInstances = new Dictionary<string, NodeBase?>();
    }

    // ── Node lifecycle ──

    /// <summary>
    /// Gets or creates the NodeBase instance for a canvas node.
    /// Returns null for variable/event nodes (handled specially).
    /// </summary>
    internal NodeBase? GetOrCreateInstance(string nodeId)
    {
        if (_nodeInstances.TryGetValue(nodeId, out var existing))
            return existing;

        if (!_nodeDefinitions.TryGetValue(nodeId, out var definition))
            return null;

        NodeBase? instance = null;
        if (definition.NodeType is not null)
        {
            instance = (NodeBase)Activator.CreateInstance(definition.NodeType)!;
            instance.NodeId = nodeId;
        }

        _nodeInstances[nodeId] = instance;
        return instance;
    }

    // ── Execution ──

    /// <summary>
    /// Executes a node by ID. Creates the context, resolves data inputs, calls ExecuteAsync.
    /// </summary>
    internal async Task ExecuteNodeByIdAsync(string nodeId)
    {
        if (!_nodeMap.TryGetValue(nodeId, out var nodeData))
            return;

        // Skip if already executed this generation (data-only dedup)
        if (RuntimeStorage.IsNodeExecuted(nodeId))
            return;

        CancellationToken.ThrowIfCancellationRequested();

        var ctx = new NodeExecutionContextImpl(nodeData, this);

        NodeStarted?.Invoke(this, new NodeExecutionEventArgs(nodeData));

        try
        {
            // Resolve data inputs (lazy upstream execution for data-only nodes)
            await ResolveAllDataInputsAsync(nodeData);

            // Get the node instance or inline executor
            if (_nodeDefinitions.TryGetValue(nodeId, out var def) && def.InlineExecutor is not null)
            {
                // Inline/lambda node
                await def.InlineExecutor(ctx, CancellationToken);
            }
            else
            {
                var instance = GetOrCreateInstance(nodeId);
                if (instance is not null)
                {
                    await instance.OnCreatedAsync(Services);
                    await instance.ExecuteAsync(ctx, CancellationToken);
                }
            }

            RuntimeStorage.MarkNodeExecuted(nodeId);
            NodeCompleted?.Invoke(this, new NodeExecutionEventArgs(nodeData));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            NodeFailed?.Invoke(this, new NodeExecutionFailedEventArgs(nodeData, ex));
            throw;
        }
    }

    // ── Data input resolution ──

    /// <summary>
    /// For each data input socket on a node, follows the upstream connection
    /// and lazily executes the source node if it hasn't run yet.
    /// </summary>
    internal async Task ResolveAllDataInputsAsync(NodeData node)
    {
        foreach (var inputSocket in node.Inputs.Where(s => !s.IsExecution))
        {
            await ResolveInputAsync(node, inputSocket.Name);
        }
    }

    /// <summary>
    /// Resolves a single input socket value by following upstream data connections.
    /// </summary>
    internal async Task<object?> ResolveInputAsync(NodeData node, string socketName)
    {
        var key = (node.Id, socketName);
        if (!_dataInputConnections.TryGetValue(key, out var source))
            return null;

        // Ensure the upstream node has executed
        if (!RuntimeStorage.IsNodeExecuted(source.sourceNodeId))
        {
            await ExecuteNodeByIdAsync(source.sourceNodeId);
        }

        // Read the upstream output value and copy to this node's input
        var value = RuntimeStorage.GetSocketValue(source.sourceNodeId, source.sourceSocket);
        RuntimeStorage.SetSocketValue(node.Id, socketName, value);
        return value;
    }

    // ── Connection lookups ──

    internal List<(string targetNodeId, string targetSocket)> GetExecutionTargets(string nodeId, string socketName)
    {
        return _execConnections.TryGetValue((nodeId, socketName), out var targets)
            ? targets
            : new List<(string, string)>();
    }

    internal StreamSocketInfo? GetStreamInfo(NodeData node, string itemSocketName)
    {
        if (!_nodeDefinitions.TryGetValue(node.Id, out var def))
            return null;
        return def.StreamSockets?.FirstOrDefault(s => s.ItemDataSocket == itemSocketName);
    }

    internal StreamMode GetStreamMode(NodeData node)
    {
        // Default to Sequential. Could be configurable per-node in the future.
        return StreamMode.Sequential;
    }

    internal T DeserializeSocketValue<T>(SocketValue socketValue)
    {
        // Delegate to SocketTypeResolver + JSON deserialization (same as current)
        // Implementation detail — reuses existing SocketValue.Json deserialization
        throw new NotImplementedException("Wire up SocketTypeResolver");
    }

    internal void RaiseFeedback(string message, NodeData node, ExecutionFeedbackType type, object? tag)
    {
        FeedbackReceived?.Invoke(this, new FeedbackMessageEventArgs(message, node, type, tag));
    }

    // ── Map builders ──

    private static Dictionary<(string, string), List<(string, string)>> BuildExecConnectionMap(
        IReadOnlyList<ConnectionData> connections)
    {
        var map = new Dictionary<(string, string), List<(string, string)>>();
        foreach (var conn in connections.Where(c => c.IsExecution))
        {
            var key = (conn.OutputNodeId, conn.OutputSocketName);
            if (!map.TryGetValue(key, out var list))
            {
                list = new List<(string, string)>();
                map[key] = list;
            }
            list.Add((conn.InputNodeId, conn.InputSocketName));
        }
        return map;
    }

    private static Dictionary<(string, string), (string, string)> BuildDataInputConnectionMap(
        IReadOnlyList<ConnectionData> connections)
    {
        var map = new Dictionary<(string, string), (string, string)>();
        foreach (var conn in connections.Where(c => !c.IsExecution))
        {
            // Each data input can have at most one upstream connection
            map[(conn.InputNodeId, conn.InputSocketName)] = (conn.OutputNodeId, conn.OutputSocketName);
        }
        return map;
    }

    private static Dictionary<string, NodeDefinition> ResolveDefinitions(
        IReadOnlyList<NodeData> nodes, INodeRegistryService registry)
    {
        var defMap = registry.Definitions.ToDictionary(d => d.Id);
        var result = new Dictionary<string, NodeDefinition>();
        foreach (var node in nodes)
        {
            if (node.DefinitionId is not null && defMap.TryGetValue(node.DefinitionId, out var def))
                result[node.Id] = def;
        }
        return result;
    }
}
```

---

## 3.3 Rewrite `NodeExecutionService`

**File**: `NodeEditor.Net/Services/Execution/Runtime/NodeExecutionService.cs` (rewrite)

The public API stays the same (`ExecuteAsync`, events), but the internals simplify dramatically.

**Current flow** (~600 lines):
1. Snapshot → Plan → RegisterEventListeners → ExecuteStepsAsync (recursive step dispatch)

**New flow** (~200 lines):
1. Snapshot → Create `ExecutionRuntime` → Seed variables → Create node instances → Execute initiators

```csharp
// Simplified new structure (pseudocode showing the key method):

public async Task ExecuteAsync(/* params */)
{
    // 1. Build runtime (creates connection maps, resolves definitions)
    var runtime = new ExecutionRuntime(nodes, connections, runtimeStorage,
        services, registry, _gate, ct);

    // Forward runtime events to service events
    runtime.NodeStarted += (s, e) => NodeStarted?.Invoke(this, e);
    runtime.NodeCompleted += (s, e) => NodeCompleted?.Invoke(this, e);
    runtime.NodeFailed += (s, e) => NodeFailed?.Invoke(this, e);
    runtime.FeedbackReceived += (s, e) => FeedbackReceived?.Invoke(this, e);

    // 2. Seed variables
    VariableNodeExecutor.SeedVariables(nodes, connections, runtimeStorage, variables);

    // 3. Register event listeners (Custom Event nodes)
    RegisterEventListeners(runtime, nodes, connections);

    // 4. Create all node instances (for DI setup)
    foreach (var node in nodes)
    {
        var instance = runtime.GetOrCreateInstance(node.Id);
        if (instance is not null)
            await instance.OnCreatedAsync(services);
    }

    // 5. Find initiator nodes and execute them
    var initiators = nodes.Where(n => n.ExecInit).ToList();
    if (options.MaxDegreeOfParallelism > 1 && initiators.Count > 1)
    {
        await Task.WhenAll(initiators.Select(n => runtime.ExecuteNodeByIdAsync(n.Id)));
    }
    else
    {
        foreach (var initiator in initiators)
            await runtime.ExecuteNodeByIdAsync(initiator.Id);
    }

    // 6. Cleanup
    foreach (var (_, instance) in runtime.NodeInstances)
        instance?.OnDisposed();
}
```

**What's removed**:
- `ExecuteStepsAsync()` — the recursive step dispatcher (handles `LayerStep`, `LoopStep`, `BranchStep`, `ParallelSteps`)
- `NodeMethodInvoker` creation and usage
- `CompositeNodeContext` creation
- `ExecutionPath.IsSignaled` checking logic
- `ResolveInputsAsync()` (replaced by `ExecutionRuntime.ResolveInputAsync()`)
- `PushGeneration`/`PopGeneration`/`ClearExecutedForNodes` for loop body management

**What stays**:
- `VariableNodeExecutor.SeedVariables()` call
- Event listener registration (Custom Event / Trigger Event)
- `ExecutionGate` integration
- Public events (`NodeStarted`, `NodeCompleted`, `NodeFailed`, `FeedbackReceived`)
- `ExecutionOptions` parameter

---

## 3.4 Simplify `ExecutionPlanner`

**File**: `NodeEditor.Net/Services/Execution/Planning/ExecutionPlanner.cs`

The planner reduces from ~544 lines to ~100 lines. Its new role is **validation only**:

**Remove**:
- `DetectLoopHeaders()` — loops are handled by node code
- `FindLoopBodyNodes()` — no more loop body extraction
- `BuildSteps()` / `BuildHierarchicalPlan()` — no more step generation
- `LoopNodeNames`, `LoopPathNames`, `ExitPathNames` conventions

**Keep/Add**:
- `ValidateGraph(nodes, connections)` → checks for:
  - Disconnected required inputs (warning)
  - Data-flow cycles (error — data-only subgraphs must be DAGs)
  - Type mismatches on connections (warning)
  - Unreachable nodes (info)
- Return a `GraphValidationResult` (errors, warnings, infos) instead of `HierarchicalPlan`

```csharp
namespace NodeEditor.Net.Services.Execution;

public sealed class ExecutionPlanner
{
    public GraphValidationResult ValidateGraph(
        IReadOnlyList<NodeData> nodes,
        IReadOnlyList<ConnectionData> connections)
    {
        var result = new GraphValidationResult();

        ValidateDataFlowAcyclicity(nodes, connections, result);
        ValidateConnectedInputs(nodes, connections, result);
        ValidateReachability(nodes, connections, result);

        return result;
    }

    // ... validation methods
}

public sealed class GraphValidationResult
{
    public List<GraphValidationMessage> Messages { get; } = new();
    public bool HasErrors => Messages.Any(m => m.Severity == ValidationSeverity.Error);
}

public sealed record GraphValidationMessage(
    ValidationSeverity Severity,
    string Message,
    string? NodeId = null);

public enum ValidationSeverity { Info, Warning, Error }
```

---

## 3.5 Simplify `ExecutionStep.cs`

**File**: `NodeEditor.Net/Services/Execution/Planning/ExecutionStep.cs`

Most step types are removed since flow control is in node code.

**Remove**:
- `LoopStep` — loops are real loops inside `ExecuteAsync()`
- `BranchStep` — branches are real if-statements inside `ExecuteAsync()`
- `HierarchicalPlan` — no more plan generation

**Keep** (may still be useful for reporting):
- `LayerStep` — for visualization of which nodes would execute in parallel
- `ParallelSteps` — for visualization
- Or remove entirely if the planner's role is purely validation

---

## 3.6 Update `ExecutionGate`

**File**: `NodeEditor.Net/Services/Execution/Runtime/ExecutionGate.cs`

**Minor update**: The gate is now checked inside `TriggerAsync()` (in `NodeExecutionContextImpl`) rather than in the step dispatcher. The gate API stays the same:
- `WaitAsync(ct)` — blocks until gate is open
- `Run()`, `Pause()`, `StepOnce()`, `Resume()`

No structural changes needed — just update the call site from `ExecuteStepsAsync` to `TriggerAsync`.

---

## 3.7 Update `BackgroundExecutionQueue` and `BackgroundExecutionWorker`

**Files**:
- `NodeEditor.Net/Services/Execution/Runtime/BackgroundExecutionQueue.cs`
- `NodeEditor.Net/Services/Execution/Runtime/BackgroundExecutionWorker.cs`

**Minor updates**: These wrap `INodeExecutionService.ExecuteAsync()` — since the service's public API is unchanged, these should work as-is. May need minor updates if `ExecutionOptions` changes.

---

## 3.8 Update `HeadlessGraphRunner`

**File**: `NodeEditor.Net/Services/Execution/Runtime/HeadlessGraphRunner.cs`

**Changes**:
- Remove `CompositeNodeContext` creation (was: `_contextFactory.CreateCompositeFromLoadedAssemblies()`)
- The `nodeContext` parameter is no longer needed — node instances are created per-node by the `ExecutionRuntime`
- Simplifies to just: extract nodes/connections from `GraphData` → call `_executionService.ExecuteAsync(nodes, connections, context, options, ct)`

---

## Flow comparison: For Loop execution

### Old system (plan-driven)

```
1. Planner: DetectLoopHeaders → finds ForLoop node
2. Planner: FindLoopBodyNodes → extracts body nodes
3. Planner: Creates LoopStep(header, loopPathSocket, exitPathSocket, body, bodyNodes)
4. Engine: ExecuteStepsAsync hits LoopStep
5. Engine: while(iteration < 10000):
     a. Execute header node via NodeMethodInvoker (reflection)
     b. Header method increments _state["{nodeId}:for"], signals LoopPath or Exit
     c. Check ExecutionPath.IsSignaled on exit → break?
     d. Check ExecutionPath.IsSignaled on loopPath → continue?
     e. PushGeneration, ClearExecutedForNodes(bodyNodeIds)
     f. ExecuteStepsAsync(body) — recursive step dispatch
     g. PopGeneration
```

### New system (coroutine-driven)

```
1. No planner involvement for loops
2. Engine: ExecuteNodeByIdAsync(forLoopNodeId)
3. Engine: Creates NodeExecutionContextImpl, calls ForLoopNode.ExecuteAsync(ctx, ct)
4. ForLoopNode.ExecuteAsync:
     var loopTimes = ctx.GetInput<int>("LoopTimes");
     for (int i = 0; i < loopTimes; i++)
     {
         ctx.SetOutput("Index", i);
         await ctx.TriggerAsync("LoopPath");  // ← runs all downstream body nodes
     }
     await ctx.TriggerAsync("Exit");           // ← runs all downstream post-loop nodes
```

The loop is a real `for` loop. `TriggerAsync("LoopPath")` suspends the loop node, runs the entire connected downstream chain to completion, then resumes. No generation tracking, no body node extraction, no `ClearExecutedForNodes`.

---

## Files impacted by this phase

| Action | File | Notes |
|--------|------|-------|
| **Create** | `NodeEditor.Net/Services/Execution/Context/NodeExecutionContextImpl.cs` | New high-level context |
| **Create** | `NodeEditor.Net/Services/Execution/Runtime/ExecutionRuntime.cs` | New internal orchestrator |
| **Rewrite** | `NodeEditor.Net/Services/Execution/Runtime/NodeExecutionService.cs` | ~600 lines → ~200 lines |
| **Rewrite** | `NodeEditor.Net/Services/Execution/Planning/ExecutionPlanner.cs` | ~544 lines → ~100 lines (validation only) |
| **Simplify** | `NodeEditor.Net/Services/Execution/Planning/ExecutionStep.cs` | Remove `LoopStep`, `BranchStep`, `HierarchicalPlan` |
| **Minor update** | `NodeEditor.Net/Services/Execution/Runtime/ExecutionGate.cs` | Call site change |
| **Minor update** | `NodeEditor.Net/Services/Execution/Runtime/HeadlessGraphRunner.cs` | Remove `CompositeNodeContext` usage |
| **No change** | `NodeEditor.Net/Services/Execution/Runtime/BackgroundExecutionQueue.cs` | Wraps service's unchanged public API |
| **No change** | `NodeEditor.Net/Services/Execution/Runtime/BackgroundExecutionWorker.cs` | Same |

## Dependencies

- Depends on Phase 1 (`NodeBase`, `INodeExecutionContext`, `INodeRuntimeStorage`, `StreamMode`)
- Depends on Phase 2 (`NodeDefinition.NodeType`, `NodeDefinition.InlineExecutor`, `NodeDefinition.StreamSockets`)
- Phase 4 depends on this (standard nodes need the new execution model to work)
