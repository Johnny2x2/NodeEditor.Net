# 6A — NodeExecutionService Rewrite

> **Phase 6 — Execution Service & Utilities**
> All sub-tasks in this phase can run fully in parallel.

## Prerequisites
- **Phase 5** complete (specifically 5B — `ExecutionRuntime` must exist)

## Can run in parallel with
- **6B** (Utility Updates)

## Deliverable

### Rewrite `NodeExecutionService`

**File**: `NodeEditor.Net/Services/Execution/Runtime/NodeExecutionService.cs`

~600 lines → ~200 lines. Public API unchanged, internals simplified.

**Current flow**: Snapshot → Plan → RegisterEventListeners → ExecuteStepsAsync (recursive step dispatch)

**New flow**: Snapshot → Create `ExecutionRuntime` → Seed variables → Create node instances → Execute initiators

```csharp
public async Task ExecuteAsync(/* params */)
{
    // 1. Build runtime
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
        await Task.WhenAll(initiators.Select(n => runtime.ExecuteNodeByIdAsync(n.Id)));
    else
        foreach (var initiator in initiators)
            await runtime.ExecuteNodeByIdAsync(initiator.Id);

    // 6. Cleanup
    foreach (var (_, instance) in runtime.NodeInstances)
        instance?.OnDisposed();
}
```

**Removed**:
- `ExecuteStepsAsync()` — recursive step dispatcher
- `NodeMethodInvoker` creation/usage
- `CompositeNodeContext` creation
- `ExecutionPath.IsSignaled` checking
- `PushGeneration`/`PopGeneration`/`ClearExecutedForNodes` for loops

**Kept**:
- `VariableNodeExecutor.SeedVariables()` call
- Event listener registration (Custom Event / Trigger Event)
- `ExecutionGate` integration
- Public events
- `ExecutionOptions` parameter

## Acceptance criteria

- [x] `ExecuteAsync` public signature unchanged
- [x] Creates `ExecutionRuntime` and delegates to it
- [x] Initiator nodes discovered by `ExecInit` flag
- [x] Parallel initiator support via `MaxDegreeOfParallelism`
- [ ] Variable seeding still works — **PARTIAL**: `VariableNodeExecutor.SeedVariables` not called in `ExecuteGraphAsync` (only in `HeadlessGraphRunner`)
- [x] Event subscriptions still fire (NodeStarted, NodeCompleted, etc.)
- [x] No references to `NodeMethodInvoker`, `CompositeNodeContext`, `ExecutionPath`
- [x] `OnDisposed()` called on all node instances after execution

### Review notes (2026-02-11)

**Status: MOSTLY COMPLETE — 1 critical gap blocks tests**

The `NodeExecutionService` rewrite is structurally complete. The `nodeContext` parameter is accepted
but ignored — the old `NodeMethodInvoker` bridge that turned `[Node]` methods into executable
delegates was removed, but `NodeDiscoveryService.BuildDefinitionsFromContext()` does not yet
generate `InlineExecutor` delegates for discovered `[Node]` methods. As a result, **all 12
`ExecutionEngineTests` fail** with `No node implementation available for 'Start'`.

This is a Phase 4 discovery gap (should create `InlineExecutor` from `[Node]` methods) that
surfaced here. Fixing it in `NodeDiscoveryService` will unblock all Phase 6 tests.
