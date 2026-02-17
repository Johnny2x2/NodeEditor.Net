# 1B — Core Interfaces (NodeBase, INodeExecutionContext, INodeRuntimeStorage)

> **Phase 1 — Core Types & Interfaces**
> All sub-tasks in this phase can run fully in parallel.

## Prerequisites
- None — these are standalone type definitions.

## Can run in parallel with
- **1A** (Utility Types)

## Deliverables

### `NodeBase` — Abstract base class for all nodes

**File**: `NodeEditor.Net/Services/Execution/Nodes/NodeBase.cs`

```csharp
namespace NodeEditor.Net.Services.Execution;

/// <summary>
/// Base class for all node implementations. Subclass this to create
/// a node with explicit sockets (defined via Configure) and an
/// execution body (ExecuteAsync).
/// </summary>
public abstract class NodeBase
{
    /// <summary>
    /// The unique instance ID of this node on the canvas.
    /// Set by the engine before execution.
    /// </summary>
    public string NodeId { get; internal set; } = string.Empty;

    /// <summary>
    /// Defines the node's metadata and sockets using the builder API.
    /// Called once during discovery/registration — NOT per execution.
    /// </summary>
    public abstract void Configure(INodeBuilder builder);

    /// <summary>
    /// Executes the node's logic. Called by the execution engine.
    /// For callable nodes, this is invoked when the node's execution input is triggered.
    /// For data-only nodes, this is invoked lazily when a downstream node reads an input.
    /// </summary>
    public abstract Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct);

    /// <summary>
    /// Optional lifecycle hook called after the node instance is created,
    /// before ExecuteAsync. Use for DI resolution and one-time setup.
    /// </summary>
    public virtual Task OnCreatedAsync(IServiceProvider services) => Task.CompletedTask;

    /// <summary>
    /// Optional cleanup hook called after execution completes.
    /// </summary>
    public virtual void OnDisposed() { }
}
```

**Design notes**:
- One instance per canvas node per execution run. State is natural class fields.
- `Configure()` is called once at registration (during discovery) to produce socket metadata. NOT called per-execution.
- `OnCreatedAsync` replaces `CompositeNodeContext` DI pattern — gives each node access to the DI container.
- `NodeId` is set by the engine so the node knows its own identity.

---

### `INodeExecutionContext` — New high-level execution API

**File**: `NodeEditor.Net/Services/Execution/Context/INodeExecutionContext.cs` (replaces existing)

```csharp
using NodeEditor.Net.Models;

namespace NodeEditor.Net.Services.Execution;

/// <summary>
/// The execution context passed to NodeBase.ExecuteAsync().
/// Provides high-level APIs for reading inputs, writing outputs,
/// triggering downstream execution paths, and streaming items.
/// </summary>
public interface INodeExecutionContext
{
    /// <summary>The identity of the currently executing node.</summary>
    NodeData Node { get; }

    /// <summary>DI service provider for resolving services during execution.</summary>
    IServiceProvider Services { get; }

    /// <summary>Cancellation token for the current execution run.</summary>
    CancellationToken CancellationToken { get; }

    // ── Data I/O ──

    T GetInput<T>(string socketName);
    object? GetInput(string socketName);
    bool TryGetInput<T>(string socketName, out T value);
    void SetOutput<T>(string socketName, T value);
    void SetOutput(string socketName, object? value);

    // ── Execution flow ──

    /// <summary>
    /// Triggers a named execution output socket. Suspends this node,
    /// executes all connected downstream callable nodes to completion,
    /// then resumes this node.
    /// </summary>
    Task TriggerAsync(string executionOutputName);

    // ── Streaming ──

    Task EmitAsync<T>(string streamItemSocket, T item);
    Task EmitAsync(string streamItemSocket, object? item);

    // ── Variables ──

    object? GetVariable(string key);
    void SetVariable(string key, object? value);

    // ── Feedback ──

    void EmitFeedback(string message, ExecutionFeedbackType type = ExecutionFeedbackType.DebugPrint,
        object? tag = null);

    // ── Event bus ──

    ExecutionEventBus EventBus { get; }

    // ── Advanced ──

    INodeRuntimeStorage RuntimeStorage { get; }
}
```

---

### `INodeRuntimeStorage` — Low-level storage (renamed from old INodeExecutionContext)

**File**: `NodeEditor.Net/Services/Execution/Context/INodeRuntimeStorage.cs`

```csharp
namespace NodeEditor.Net.Services.Execution;

/// <summary>
/// Low-level runtime storage for node execution state.
/// Used internally by the execution engine. Most node implementations
/// should use INodeExecutionContext's high-level APIs instead.
/// </summary>
public interface INodeRuntimeStorage
{
    bool TryGetSocketValue(string nodeId, string socketName, out object? value);
    object? GetSocketValue(string nodeId, string socketName);
    void SetSocketValue(string nodeId, string socketName, object? value);

    bool IsNodeExecuted(string nodeId);
    void MarkNodeExecuted(string nodeId);
    void ClearNodeExecuted(string nodeId);

    object? GetVariable(string key);
    void SetVariable(string key, object? value);

    int CurrentGeneration { get; }
    void PushGeneration();
    void PopGeneration();
    void ClearExecutedForNodes(IEnumerable<string> nodeIds);

    INodeRuntimeStorage CreateChild(string scopeName, bool inheritVariables = true);

    ExecutionEventBus EventBus { get; }
}
```

## Acceptance criteria

- [x] `NodeBase` compiles as abstract class with `Configure`, `ExecuteAsync`, `OnCreatedAsync`, `OnDisposed`
- [x] `INodeExecutionContext` compiles with all 15+ members
- [x] `INodeRuntimeStorage` compiles with all storage/lifecycle members
- [x] New `INodeExecutionContext` replaces the old one (old becomes `INodeRuntimeStorage`)
- [x] Solution builds (old implementation `NodeExecutionContext` still compiles against renamed interface)
