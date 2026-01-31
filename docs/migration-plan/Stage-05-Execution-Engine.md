# Stage 05 — Execution Engine

## Status: 🟡 Partially Complete (Design Updated)

### What's Done
- ✅ `NodeData` and `ConnectionData` models ready for execution
- ✅ `SocketValue` with type-safe serialization

### What's Remaining
- ❌ `NodeExecutionService` - main execution orchestrator
- ❌ `INodeExecutionContext` - variable storage and outputs
- ❌ Port `Resolve()` algorithm from legacy `NodeGraph.cs`
- ❌ Port `FeedbackInfo` mechanism for execution flow control
- ❌ Node method binding via reflection
- ❌ Parallel + async execution support
- ❌ Background execution support
- ❌ Grouped node execution (subgraphs)

## Goal
Port NodeManager execution logic into a service layer compatible with MAUI/Blazor, and add:
- Parallel and async node execution
- Background execution (detached tasks)
- Grouped nodes (subgraphs) with isolated execution contexts

## Deliverables
- NodeExecutionService (async + parallel aware)
- Execution events and cancellation support
- Execution planner (dependency graph + grouping)
- Group execution API (subgraph execution)
- Background execution queue

## Tasks
1. Move execution flow from NodeManager into NodeExecutionService.
2. Ensure cancellation token support remains intact.
3. Add events for start, node executed, finish.
4. Add execution planner that computes dependency layers and group boundaries.
5. Add async/parallel execution paths for independent nodes.
6. Add background execution queue with cancellation support.
7. Add grouped node support with nested execution contexts.

## Acceptance Criteria
- Execution produces the same outputs as the WinForms version (single-threaded mode).
- Parallel execution yields deterministic results for graphs without shared mutable state.
- Feedback events propagate correctly.
- Background runs can be started, observed, and canceled.
- Grouped nodes execute as a subgraph with isolated context.

### Testing Parameters
- NUnit/xUnit deterministic execution tests match WinForms outputs for sample graphs.
- NUnit/xUnit cancellation test: execution stops within 100ms after token cancel.
- NUnit/xUnit parallel test: independent nodes execute concurrently, order-agnostic outputs equal.
- NUnit/xUnit background test: background job continues after UI thread yields and can be canceled.
- NUnit/xUnit grouped nodes test: group outputs match executing the same subgraph standalone.

## Dependencies
Stage 02.

## Risks / Notes
- Avoid using UI thread assumptions.

## Architecture Notes
Execution should be **purely model-driven**:
- `NodeExecutionService` consumes `NodeData`, `ConnectionData`, and context.
- The execution engine must **not** know about UI components or ViewModels.
- Provide an **execution context** abstraction for input/output values and variables.
- Add an **execution planner** that produces dependency layers for parallel execution.
- Add **group nodes** (subgraphs) that run with a child context, optionally inheriting variables.
- Background execution uses a scheduler/queue independent of the UI thread.

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
6. **Parallel + async**
	- Build dependency layers for nodes without direct/indirect dependencies.
	- Execute each layer with `Task.WhenAll`.
	- Provide deterministic fallback (`ExecutionMode.Sequential`).
7. **Background execution**
	- Execute on a dedicated scheduler (queue + worker).
	- Allow cancellation and progress events for background jobs.
8. **Grouped nodes**
	- Represent a group as a special node containing a subgraph.
	- Run group with a child context and return outputs to parent.

## Code Examples

### Execution service skeleton
```csharp
public sealed class NodeExecutionService
{
	 public event EventHandler<NodeData>? NodeStarted;
	 public event EventHandler<NodeData>? NodeCompleted;
	 public event EventHandler<Exception>? ExecutionFailed;
	 public event EventHandler<ExecutionLayerEventArgs>? LayerStarted;
	 public event EventHandler<ExecutionLayerEventArgs>? LayerCompleted;

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

### Parallel execution layer planner
```csharp
public sealed record ExecutionPlan(IReadOnlyList<ExecutionLayer> Layers);

public sealed record ExecutionLayer(IReadOnlyList<NodeData> Nodes);

public sealed class ExecutionPlanner
{
	public ExecutionPlan BuildPlan(IReadOnlyList<NodeData> nodes, IReadOnlyList<ConnectionData> connections)
	{
		// 1) Build dependency graph (data + exec edges)
		// 2) Topological sort into layers of independent nodes
		// 3) Return layers for parallel execution
		throw new NotImplementedException();
	}
}
```

### Parallel + async execution
```csharp
public enum ExecutionMode
{
	Sequential,
	Parallel
}

public sealed record NodeExecutionOptions(
	ExecutionMode Mode,
	bool AllowBackground,
	int MaxDegreeOfParallelism);

public async Task ExecutePlannedAsync(
	ExecutionPlan plan,
	IReadOnlyList<ConnectionData> connections,
	INodeExecutionContext context,
	NodeExecutionOptions options,
	CancellationToken token)
{
	foreach (var layer in plan.Layers)
	{
		token.ThrowIfCancellationRequested();
		LayerStarted?.Invoke(this, new ExecutionLayerEventArgs(layer));

		if (options.Mode == ExecutionMode.Sequential)
		{
			foreach (var node in layer.Nodes)
			{
				await ExecuteNodeAsync(node, connections, context, token);
			}
		}
		else
		{
			using var throttler = new SemaphoreSlim(options.MaxDegreeOfParallelism);
			var tasks = layer.Nodes.Select(async node =>
			{
				await throttler.WaitAsync(token);
				try { await ExecuteNodeAsync(node, connections, context, token); }
				finally { throttler.Release(); }
			});
			await Task.WhenAll(tasks);
		}

		LayerCompleted?.Invoke(this, new ExecutionLayerEventArgs(layer));
	}
}
```

### Background execution queue
```csharp
public sealed record ExecutionJob(Guid Id, ExecutionPlan Plan, INodeExecutionContext Context, NodeExecutionOptions Options);

public sealed class BackgroundExecutionQueue
{
	private readonly Channel<ExecutionJob> _queue = Channel.CreateUnbounded<ExecutionJob>();

	public ValueTask EnqueueAsync(ExecutionJob job) => _queue.Writer.WriteAsync(job);

	public IAsyncEnumerable<ExecutionJob> DequeueAllAsync(CancellationToken token) => _queue.Reader.ReadAllAsync(token);
}

public sealed class BackgroundExecutionWorker
{
	private readonly BackgroundExecutionQueue _queue;
	private readonly NodeExecutionService _executor;

	public BackgroundExecutionWorker(BackgroundExecutionQueue queue, NodeExecutionService executor)
	{
		_queue = queue;
		_executor = executor;
	}

	public async Task RunAsync(CancellationToken token)
	{
		await foreach (var job in _queue.DequeueAllAsync(token))
		{
			await _executor.ExecutePlannedAsync(job.Plan, Array.Empty<ConnectionData>(), job.Context, job.Options, token);
		}
	}
}
```

### Grouped nodes (subgraph execution)
```csharp
public sealed record GroupNodeData(
	string Id,
	string Name,
	IReadOnlyList<NodeData> Nodes,
	IReadOnlyList<ConnectionData> Connections,
	IReadOnlyList<SocketData> Inputs,
	IReadOnlyList<SocketData> Outputs);

public async Task ExecuteGroupAsync(
	GroupNodeData group,
	INodeExecutionContext parentContext,
	CancellationToken token)
{
	var childContext = parentContext.CreateChild(scopeName: group.Id);
	var plan = _planner.BuildPlan(group.Nodes, group.Connections);
	await ExecutePlannedAsync(plan, group.Connections, childContext, _defaultOptions, token);

	// Copy group outputs back to parent context
	foreach (var output in group.Outputs)
	{
		var value = childContext.GetSocketValue(group.Id, output.Name);
		parentContext.SetSocketValue(group.Id, output.Name, value);
	}
}
```

## Missing Architecture Gaps (to close in this stage)
- **Type conversion**: from serialized values to runtime types
- **Error routing**: determine how execution errors are surfaced in UI
- **Loop protection**: guard against infinite loops with iteration limits
- **Thread safety**: enforce immutable node inputs or guard shared state
- **Determinism**: optional deterministic mode for parallel execution
- **Group IO**: mapping group inputs/outputs to internal sockets

## Implementation Notes (for next developer)

### Legacy Code to Port
The execution logic lives in the original `NodeEditor` project:
- `NodeManager.cs` - Main execution orchestrator
- `NodeGraph.cs` - Contains `Resolve()` method for dependency resolution
- `ExecutionPath.cs` - Tracks execution flow
- `FeedbackType.cs` - Enum for execution control (Break, Continue, Wait, True, False, None)

### Key Algorithm: Resolve()
The `Resolve()` method in `NodeGraph.cs` recursively resolves node inputs:
1. Find connections feeding into the target socket
2. Execute the source node if not yet executed
3. Cache the output value
4. Return the value for the input socket

### FeedbackInfo Pattern
Legacy uses `FeedbackInfo` to control execution flow:
```csharp
public struct FeedbackInfo
{
    public FeedbackType Type;  // Break, Continue, Wait, True, False, None
    public object Value;
}
```
This allows conditional nodes to signal which execution path to follow.

### Recommended Service Structure
```
NodeEditor.Blazor/Services/Execution/
├── NodeExecutionService.cs      # Main orchestrator
├── INodeExecutionContext.cs     # Variable storage interface
├── NodeExecutionContext.cs      # Default implementation
├── NodeMethodInvoker.cs         # Reflection-based method binding
├── ExecutionResult.cs           # Success/failure with values
└── FeedbackInfo.cs             # Port from legacy
├── ExecutionPlanner.cs          # Dependency planner (layers)
├── ExecutionMode.cs             # Sequential/Parallel
├── NodeExecutionOptions.cs      # Options (parallelism/background)
├── BackgroundExecutionQueue.cs  # Background queue
├── GroupNodeData.cs             # Group/subgraph model
```

### INodesContext Equivalent
Legacy uses `INodesContext` (implemented by `StandardNodeContext`) for node method implementations.
Need to create `INodeContext` interface in Blazor that:
- Provides methods for all standard nodes (math, conditions, etc.)
- Is injectable and testable
- Supports async operations

## Checklist
- [ ] Execution is deterministic and matches WinForms output
- [ ] Cancellation is respected at every node boundary
- [ ] No UI references in execution code
- [ ] `Resolve()` algorithm ported correctly
- [ ] `FeedbackInfo` flow control working
- [ ] Error aggregation and reporting
- [ ] Parallel execution supported via dependency layers
- [ ] Async node invocation supported end-to-end
- [ ] Background execution queue implemented
- [ ] Grouped node execution implemented with child contexts
