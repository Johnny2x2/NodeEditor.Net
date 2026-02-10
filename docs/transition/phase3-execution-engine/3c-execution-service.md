# 3C — NodeExecutionService Rewrite

> **Parallelism**: Depends on **3B**. Can run in parallel with **3D** once 3B API is known.

## Prerequisites
- **3B** — `ExecutionRuntime` must exist (this service creates and orchestrates it)
- **Phase 1** and **Phase 2** complete

## Can run in parallel with
- **3D** (Utility Updates) — once 3B's API shape is known

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

- [ ] `ExecuteAsync` public signature unchanged
- [ ] Creates `ExecutionRuntime` and delegates to it
- [ ] Initiator nodes discovered by `ExecInit` flag
- [ ] Parallel initiator support via `MaxDegreeOfParallelism`
- [ ] Variable seeding still works
- [ ] Event subscriptions still fire (NodeStarted, NodeCompleted, etc.)
- [ ] No references to `NodeMethodInvoker`, `CompositeNodeContext`, `ExecutionPath`
- [ ] `OnDisposed()` called on all node instances after execution
