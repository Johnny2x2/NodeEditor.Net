# Phase 1 — Core Abstractions

> **Goal**: Define the foundational types that the entire new system is built on: `NodeBase`, `NodeBuilder`, and the new `INodeExecutionContext`.

## New files to create

### 1.1 `NodeBase` — Abstract base class for all nodes

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
- `Configure()` is called once at registration (during discovery) to produce socket metadata. It is NOT called per-execution.
- `OnCreatedAsync` replaces the `CompositeNodeContext` DI pattern — gives each node access to the DI container individually.
- `NodeId` is set by the engine so the node knows its own identity (needed for logging, feedback, etc.).

---

### 1.2 `INodeBuilder` and `NodeBuilder` — Fluent socket/metadata definition

**File**: `NodeEditor.Net/Services/Execution/Nodes/INodeBuilder.cs`

```csharp
using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution;

/// <summary>
/// Fluent API for defining a node's metadata and sockets.
/// Used inside NodeBase.Configure() and as a standalone factory.
/// </summary>
public interface INodeBuilder
{
    // ── Metadata ──
    INodeBuilder Name(string name);
    INodeBuilder Category(string category);
    INodeBuilder Description(string description);

    // ── Execution sockets ──
    /// <summary>Marks this node as callable (auto-adds Enter input + Exit output execution sockets).</summary>
    INodeBuilder Callable();
    /// <summary>Marks as an execution initiator (no Enter input, just Exit output). Implies callable.</summary>
    INodeBuilder ExecutionInitiator();
    /// <summary>Adds a named execution input socket (for multi-input callable nodes).</summary>
    INodeBuilder ExecutionInput(string name);
    /// <summary>Adds a named execution output socket (for branching, loops, etc.).</summary>
    INodeBuilder ExecutionOutput(string name);

    // ── Data sockets ──
    INodeBuilder Input<T>(string name, T? defaultValue = default, SocketEditorHint? editorHint = null);
    INodeBuilder Input(string name, string typeName, SocketValue? defaultValue = null, SocketEditorHint? editorHint = null);
    INodeBuilder Output<T>(string name);
    INodeBuilder Output(string name, string typeName);

    // ── Streaming ──
    /// <summary>
    /// Declares a streaming output: a per-item execution path + a data output for the item +
    /// an optional completed execution path.
    /// </summary>
    INodeBuilder StreamOutput<T>(string itemSocketName, string onItemExecName = "OnItem",
        string? completedExecName = "Completed");
    INodeBuilder StreamOutput(string itemSocketName, string typeName,
        string onItemExecName = "OnItem", string? completedExecName = "Completed");

    // ── Inline execution (alternative to subclassing) ──
    INodeBuilder OnExecute(Func<INodeExecutionContext, CancellationToken, Task> executor);
}
```

**File**: `NodeEditor.Net/Services/Execution/Nodes/NodeBuilder.cs`

```csharp
using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution;

/// <summary>
/// Builds a NodeDefinition from fluent configuration.
/// </summary>
public sealed class NodeBuilder : INodeBuilder
{
    private string _name = "Node";
    private string _category = "General";
    private string _description = "";
    private bool _callable;
    private bool _execInit;
    private readonly List<SocketData> _inputs = new();
    private readonly List<SocketData> _outputs = new();
    private Func<INodeExecutionContext, CancellationToken, Task>? _inlineExecutor;

    // Metadata about streaming sockets (for engine to know which outputs are streaming)
    private readonly List<StreamSocketInfo> _streamSockets = new();

    // The NodeBase type this builder was configured from (null for inline/lambda nodes)
    internal Type? NodeType { get; set; }

    private NodeBuilder() { }

    /// <summary>Static entry point for standalone (non-subclass) node creation.</summary>
    public static NodeBuilder Create(string name)
    {
        return new NodeBuilder { _name = name };
    }

    /// <summary>Creates a builder for a NodeBase subclass during discovery.</summary>
    internal static NodeBuilder CreateForType(Type nodeType)
    {
        return new NodeBuilder { NodeType = nodeType };
    }

    // ── Metadata ──
    public INodeBuilder Name(string name) { _name = name; return this; }
    public INodeBuilder Category(string category) { _category = category; return this; }
    public INodeBuilder Description(string description) { _description = description; return this; }

    // ── Execution sockets ──
    public INodeBuilder Callable()
    {
        _callable = true;
        AddSocketIfMissing(_inputs, new SocketData("Enter", ExecutionSocketTypeName, true, true));
        AddSocketIfMissing(_outputs, new SocketData("Exit", ExecutionSocketTypeName, false, true));
        return this;
    }

    public INodeBuilder ExecutionInitiator()
    {
        _callable = true;
        _execInit = true;
        // No Enter socket for initiators — they start execution chains
        AddSocketIfMissing(_outputs, new SocketData("Exit", ExecutionSocketTypeName, false, true));
        return this;
    }

    public INodeBuilder ExecutionInput(string name)
    {
        _callable = true;
        AddSocketIfMissing(_inputs, new SocketData(name, ExecutionSocketTypeName, true, true));
        return this;
    }

    public INodeBuilder ExecutionOutput(string name)
    {
        AddSocketIfMissing(_outputs, new SocketData(name, ExecutionSocketTypeName, false, true));
        return this;
    }

    // ── Data sockets ──
    public INodeBuilder Input<T>(string name, T? defaultValue = default, SocketEditorHint? editorHint = null)
    {
        var socketValue = defaultValue is not null ? SocketValue.From(defaultValue) : null;
        AddSocketIfMissing(_inputs, new SocketData(name, typeof(T).FullName!, true, false, socketValue, editorHint));
        return this;
    }

    public INodeBuilder Input(string name, string typeName, SocketValue? defaultValue = null, SocketEditorHint? editorHint = null)
    {
        AddSocketIfMissing(_inputs, new SocketData(name, typeName, true, false, defaultValue, editorHint));
        return this;
    }

    public INodeBuilder Output<T>(string name)
    {
        AddSocketIfMissing(_outputs, new SocketData(name, typeof(T).FullName!, false, false));
        return this;
    }

    public INodeBuilder Output(string name, string typeName)
    {
        AddSocketIfMissing(_outputs, new SocketData(name, typeName, false, false));
        return this;
    }

    // ── Streaming ──
    public INodeBuilder StreamOutput<T>(string itemSocketName, string onItemExecName = "OnItem",
        string? completedExecName = "Completed")
    {
        return StreamOutput(itemSocketName, typeof(T).FullName!, onItemExecName, completedExecName);
    }

    public INodeBuilder StreamOutput(string itemSocketName, string typeName,
        string onItemExecName = "OnItem", string? completedExecName = "Completed")
    {
        // Data output for the current item
        AddSocketIfMissing(_outputs, new SocketData(itemSocketName, typeName, false, false));
        // Execution output: fires per-item
        AddSocketIfMissing(_outputs, new SocketData(onItemExecName, ExecutionSocketTypeName, false, true));
        // Execution output: fires once after all items (optional)
        if (completedExecName is not null)
            AddSocketIfMissing(_outputs, new SocketData(completedExecName, ExecutionSocketTypeName, false, true));

        _streamSockets.Add(new StreamSocketInfo(itemSocketName, onItemExecName, completedExecName));
        return this;
    }

    // ── Inline execution ──
    public INodeBuilder OnExecute(Func<INodeExecutionContext, CancellationToken, Task> executor)
    {
        _inlineExecutor = executor;
        return this;
    }

    // ── Build ──

    /// <summary>
    /// Produces a NodeDefinition from the builder configuration.
    /// The definition includes a Factory delegate for stamping out NodeData instances.
    /// </summary>
    public NodeDefinition Build()
    {
        var id = BuildDefinitionId();
        var inputsSnapshot = _inputs.ToArray().AsReadOnly();
        var outputsSnapshot = _outputs.ToArray().AsReadOnly();
        var name = _name;
        var callable = _callable;
        var execInit = _execInit;

        return new NodeDefinition(
            Id: id,
            Name: name,
            Category: _category,
            Description: _description,
            Inputs: inputsSnapshot,
            Outputs: outputsSnapshot,
            Factory: () => new NodeData(
                Id: Guid.NewGuid().ToString("N"),
                Name: name,
                Callable: callable,
                ExecInit: execInit,
                Inputs: inputsSnapshot,
                Outputs: outputsSnapshot,
                DefinitionId: id),
            NodeType: NodeType,
            InlineExecutor: _inlineExecutor,
            StreamSockets: _streamSockets.Count > 0 ? _streamSockets.AsReadOnly() : null);
    }

    private string BuildDefinitionId()
    {
        if (NodeType is not null)
            return $"{NodeType.FullName}";
        // For inline/lambda nodes, use name as a default (callers should ensure uniqueness)
        return _name;
    }

    private static void AddSocketIfMissing(List<SocketData> list, SocketData socket)
    {
        if (!list.Any(s => s.Name == socket.Name && s.IsInput == socket.IsInput))
            list.Add(socket);
    }

    private static readonly string ExecutionSocketTypeName =
        "NodeEditor.Net.Services.Execution.ExecutionSocket";
}

/// <summary>
/// Metadata about a streaming socket group declared via StreamOutput().
/// </summary>
public sealed record StreamSocketInfo(
    string ItemDataSocket,
    string OnItemExecSocket,
    string? CompletedExecSocket);
```

**Design notes**:
- `NodeBuilder` is both the implementation for use inside `Configure()` AND a standalone factory via `NodeBuilder.Create("name")`.
- `Build()` produces a `NodeDefinition` — same record the UI catalog, serializer, and `Factory` delegate depend on.
- `ExecutionSocketTypeName` is a new constant replacing `typeof(ExecutionPath).FullName!` since `ExecutionPath` will be removed.
- `StreamSocketInfo` captures which sockets form a streaming group, so the engine knows how `EmitAsync` should work.
- `SocketValue.From(defaultValue)` is assumed to exist or will be added — serializes a default value to the `SocketValue` JSON format.

---

### 1.3 `INodeExecutionContext` — New execution context interface

**File**: `NodeEditor.Net/Services/Execution/Context/INodeExecutionContext.cs` (replaces existing)

The existing `INodeExecutionContext` is low-level storage (socket values, executed flags, loop state). The new version is the **primary API nodes interact with** during execution.

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

    /// <summary>Reads a typed value from an input socket.</summary>
    T GetInput<T>(string socketName);

    /// <summary>Reads an untyped value from an input socket.</summary>
    object? GetInput(string socketName);

    /// <summary>Tries to read a typed value from an input socket. Returns false if not connected or no value.</summary>
    bool TryGetInput<T>(string socketName, out T value);

    /// <summary>Writes a typed value to an output socket, making it available to downstream data consumers.</summary>
    void SetOutput<T>(string socketName, T value);

    /// <summary>Writes an untyped value to an output socket.</summary>
    void SetOutput(string socketName, object? value);

    // ── Execution flow ──

    /// <summary>
    /// Triggers a named execution output socket. Suspends this node,
    /// executes all connected downstream callable nodes to completion,
    /// then resumes this node. This is the core mechanism for control flow.
    /// </summary>
    Task TriggerAsync(string executionOutputName);

    // ── Streaming ──

    /// <summary>
    /// Emits a single item through a streaming output. Sets the item value
    /// on the associated data output socket, then triggers the per-item
    /// execution path. Behavior depends on stream mode:
    /// - Sequential: waits for downstream to complete before returning
    /// - FireAndForget: returns immediately, downstream runs concurrently
    /// </summary>
    Task EmitAsync<T>(string streamItemSocket, T item);

    /// <summary>Emits an untyped item through a streaming output.</summary>
    Task EmitAsync(string streamItemSocket, object? item);

    // ── Variables ──

    /// <summary>Gets a graph variable value by key.</summary>
    object? GetVariable(string key);

    /// <summary>Sets a graph variable value by key.</summary>
    void SetVariable(string key, object? value);

    // ── Feedback ──

    /// <summary>Sends a feedback message (debug print, warning, error) to the execution host.</summary>
    void EmitFeedback(string message, ExecutionFeedbackType type = ExecutionFeedbackType.DebugPrint,
        object? tag = null);

    // ── Event bus ──

    /// <summary>Gets the event bus for Custom Event / Trigger Event nodes.</summary>
    ExecutionEventBus EventBus { get; }

    // ── Advanced / low-level ──

    /// <summary>
    /// Access to the low-level runtime storage (socket values, executed flags).
    /// Used by the engine and advanced scenarios. Most nodes should use
    /// the high-level APIs above instead.
    /// </summary>
    INodeRuntimeStorage RuntimeStorage { get; }
}
```

---

### 1.4 `INodeRuntimeStorage` — Low-level storage (renamed from old INodeExecutionContext)

**File**: `NodeEditor.Net/Services/Execution/Context/INodeRuntimeStorage.cs`

The existing `INodeExecutionContext` becomes `INodeRuntimeStorage` — it's the engine's internal bookkeeping layer. Nodes don't interact with it directly (except in advanced scenarios).

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

---

### 1.5 `ExecutionSocket` — Marker type replacing `ExecutionPath`

**File**: `NodeEditor.Net/Services/Execution/Helpers/ExecutionSocket.cs`

```csharp
namespace NodeEditor.Net.Services.Execution;

/// <summary>
/// Marker type for execution sockets. Unlike the old ExecutionPath (which carried
/// IsSignaled state), this is purely a type marker used in SocketData.TypeName
/// to distinguish execution sockets from data sockets. Flow control is handled
/// by TriggerAsync() on INodeExecutionContext, not by signaling objects.
/// </summary>
public static class ExecutionSocket
{
    public static readonly string TypeName = "NodeEditor.Net.Services.Execution.ExecutionSocket";
}
```

---

### 1.6 `StreamMode` enum

**File**: `NodeEditor.Net/Services/Execution/Nodes/StreamMode.cs`

```csharp
namespace NodeEditor.Net.Services.Execution;

/// <summary>
/// Controls how streaming emissions interact with downstream execution.
/// </summary>
public enum StreamMode
{
    /// <summary>
    /// Each EmitAsync call waits for downstream nodes to complete before returning.
    /// The node processes items sequentially.
    /// </summary>
    Sequential,

    /// <summary>
    /// EmitAsync returns immediately. Downstream nodes run concurrently.
    /// The node continues producing items without waiting.
    /// </summary>
    FireAndForget
}
```

---

## Example: What a node looks like in the new system

### Control-flow node (ForLoop)

```csharp
public sealed class ForLoopNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("For Loop")
            .Category("Conditions")
            .Description("Iterates a fixed number of times.")
            .Callable()
            .Input<int>("LoopTimes", 10)
            .Output<int>("Index")
            .ExecutionOutput("LoopPath")
            .ExecutionOutput("Exit");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var loopTimes = context.GetInput<int>("LoopTimes");

        for (int i = 0; i < loopTimes && !ct.IsCancellationRequested; i++)
        {
            context.SetOutput("Index", i);
            await context.TriggerAsync("LoopPath");
        }

        await context.TriggerAsync("Exit");
    }
}
```

### Data node (inline lambda)

```csharp
// Registered in StandardDataNodes.cs
NodeBuilder.Create("Abs")
    .Category("Numbers")
    .Description("Returns the absolute value.")
    .Input<double>("Value")
    .Output<double>("Result")
    .OnExecute(async (ctx, ct) =>
    {
        var value = ctx.GetInput<double>("Value");
        ctx.SetOutput("Result", Math.Abs(value));
    })
    .Build();
```

### Streaming node (LLM Chat)

```csharp
public sealed class LlmChatStreamNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("LLM Chat Stream")
            .Category("AI")
            .Description("Streams chat tokens from an LLM.")
            .Callable()
            .Input<string>("Prompt")
            .Output<string>("FullResponse")
            .StreamOutput<string>("Token", "OnToken", "Completed");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var prompt = context.GetInput<string>("Prompt");
        var sb = new StringBuilder();

        await foreach (var token in GetTokensAsync(prompt, ct))
        {
            sb.Append(token);
            await context.EmitAsync("Token", token);
        }

        context.SetOutput("FullResponse", sb.ToString());
        await context.TriggerAsync("Completed");
    }
}
```

---

## Files impacted by this phase

| Action | File | Notes |
|--------|------|-------|
| **Create** | `NodeEditor.Net/Services/Execution/Nodes/NodeBase.cs` | Abstract base class |
| **Create** | `NodeEditor.Net/Services/Execution/Nodes/INodeBuilder.cs` | Builder interface |
| **Create** | `NodeEditor.Net/Services/Execution/Nodes/NodeBuilder.cs` | Builder implementation |
| **Create** | `NodeEditor.Net/Services/Execution/Nodes/StreamMode.cs` | Enum |
| **Create** | `NodeEditor.Net/Services/Execution/Helpers/ExecutionSocket.cs` | Marker type |
| **Create** | `NodeEditor.Net/Services/Execution/Context/INodeRuntimeStorage.cs` | Low-level storage (renamed from old interface) |
| **Replace** | `NodeEditor.Net/Services/Execution/Context/INodeExecutionContext.cs` | Complete rewrite — new high-level API |

## Dependencies

- Phase 1 has **no dependencies** on other phases — it only adds new types.
- All subsequent phases depend on Phase 1.
- `NodeBuilder.Build()` produces `NodeDefinition` — requires `NodeDefinition` to be extended (Phase 2), but the builder code can be written first and updated when the record changes.
