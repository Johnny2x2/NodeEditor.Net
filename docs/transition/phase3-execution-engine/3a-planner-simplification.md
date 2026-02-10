# 3A — Planner Simplification & ExecutionStep Cleanup

> **Parallelism**: Can run in parallel with **3B**. No dependency on new runtime types.

## Prerequisites
- **Phase 1** complete (to know what `INodeRuntimeStorage` replaces)

## Can run in parallel with
- **3B** (Runtime & Context)

## Deliverables

### Simplify `ExecutionPlanner` — validation only

**File**: `NodeEditor.Net/Services/Execution/Planning/ExecutionPlanner.cs`

Reduce from ~544 lines to ~100 lines. New role is **graph validation**, not plan generation.

**Remove**:
- `DetectLoopHeaders()` — loops handled by node code
- `FindLoopBodyNodes()` — no more loop body extraction
- `BuildSteps()` / `BuildHierarchicalPlan()` — no step generation
- `LoopNodeNames`, `LoopPathNames`, `ExitPathNames` conventions

**Replace with**:
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

    private void ValidateDataFlowAcyclicity(IReadOnlyList<NodeData> nodes,
        IReadOnlyList<ConnectionData> connections, GraphValidationResult result)
    {
        // Topological sort on data-only connections
        // If cycle found → Error: "Data-flow cycle detected involving nodes: ..."
    }

    private void ValidateConnectedInputs(IReadOnlyList<NodeData> nodes,
        IReadOnlyList<ConnectionData> connections, GraphValidationResult result)
    {
        // For each required input socket without a connection or default → Warning
    }

    private void ValidateReachability(IReadOnlyList<NodeData> nodes,
        IReadOnlyList<ConnectionData> connections, GraphValidationResult result)
    {
        // BFS from initiator nodes → any unreachable callable node → Info
    }
}

public sealed class GraphValidationResult
{
    public List<GraphValidationMessage> Messages { get; } = new();
    public bool HasErrors => Messages.Any(m => m.Severity == ValidationSeverity.Error);
}

public sealed record GraphValidationMessage(
    ValidationSeverity Severity, string Message, string? NodeId = null);

public enum ValidationSeverity { Info, Warning, Error }
```

### Simplify `ExecutionStep.cs`

**File**: `NodeEditor.Net/Services/Execution/Planning/ExecutionStep.cs`

**Remove**:
- `LoopStep` — loops are real loops inside `ExecuteAsync()`
- `BranchStep` — branches are real if-statements
- `HierarchicalPlan` — no plan generation

**Keep or remove entirely**: `LayerStep` and `ParallelSteps` may still be useful for visualization of theoretical parallelism, but are optional. Recommend removing to keep clean.

## Acceptance criteria

- [ ] `ExecutionPlanner.ValidateGraph()` returns a `GraphValidationResult`
- [ ] Data-flow cycle detection works on data-only connections
- [ ] No references to `LoopStep`, `BranchStep`, `HierarchicalPlan` remain
- [ ] `GraphValidationResult`, `GraphValidationMessage`, `ValidationSeverity` compile clean
- [ ] Old planner tests updated or removed as needed
