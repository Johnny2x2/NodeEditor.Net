# Stage 05 — Execution Engine

## Goal
Port NodeManager execution logic into a service layer compatible with MAUI/Blazor.

## Deliverables
- NodeExecutionService
- Execution events and cancellation support

## Tasks
1. Move execution flow from NodeManager into NodeExecutionService.
2. Ensure cancellation token support remains intact.
3. Add events for start, node executed, finish.

## Acceptance Criteria
- Execution produces the same outputs as the WinForms version.
- Feedback events propagate correctly.

### Testing Parameters
- NUnit/xUnit deterministic execution tests match WinForms outputs for sample graphs.
- NUnit/xUnit cancellation test: execution stops within 100ms after token cancel.

## Dependencies
Stage 02.

## Risks / Notes
- Avoid using UI thread assumptions.

## Architecture Notes
Execution should be **purely model-driven**:
- `NodeExecutionService` consumes `NodeData`, `ConnectionData`, and context.
- The execution engine must **not** know about UI components or ViewModels.
- Provide an **execution context** abstraction for input/output values and variables.

## Detailed Tasks (Expanded)
1. **Execution context**
	- Provide variable storage and node outputs.
2. **Node invocation**
	- Resolve node method binding via attributes (`NodeAttribute`).
	- Construct input parameters from socket values.
3. **Execution path traversal**
	- Separate execution sockets vs data sockets.
	- Execution sockets drive ordering.
4. **Events and logging**
	- Node started, node finished, error encountered.
5. **Cancellation support**
	- Respect `CancellationToken` in loops and long-running nodes.

## Code Examples

### Execution service skeleton
```csharp
public sealed class NodeExecutionService
{
	 public event EventHandler<NodeData>? NodeStarted;
	 public event EventHandler<NodeData>? NodeCompleted;
	 public event EventHandler<Exception>? ExecutionFailed;

	 public async Task ExecuteAsync(
		  IReadOnlyList<NodeData> nodes,
		  IReadOnlyList<ConnectionData> connections,
		  INodeExecutionContext context,
		  CancellationToken token)
	 {
		  // locate entry nodes
		  var entryNodes = nodes.Where(n => n.ExecInit).ToList();
		  foreach (var node in entryNodes)
		  {
				token.ThrowIfCancellationRequested();
				NodeStarted?.Invoke(this, node);
				await ExecuteNodeAsync(node, connections, context, token);
				NodeCompleted?.Invoke(this, node);
		  }
	 }
}
```

## Missing Architecture Gaps (to close in this stage)
- **Type conversion**: from serialized values to runtime types
- **Error routing**: determine how execution errors are surfaced in UI
- **Loop protection**: guard against infinite loops with iteration limits

## Checklist
- [ ] Execution is deterministic and matches WinForms output
- [ ] Cancellation is respected at every node boundary
- [ ] No UI references in execution code
