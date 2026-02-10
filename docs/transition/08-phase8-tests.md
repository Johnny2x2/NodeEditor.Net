# Phase 8 — Tests

> **Goal**: Rewrite execution engine tests, add streaming tests, and update all tests that reference removed types. The test suite should verify the complete new system end-to-end.

## 8.1 Test file inventory

**File**: `NodeEditor.Blazor.Tests/` — all test files and their expected impact:

| Test file | Impact | Action |
|-----------|--------|--------|
| `ExecutionEngineTests.cs` | **Heavy** — tests execution dispatch, loops, branches, data flow | Full rewrite |
| `NodeRegistryTests.cs` | **Medium** — tests discovery + registration | Rewrite discovery tests |
| `NodeAdapterTests.cs` | **Low** — legacy adapter | Minor updates (type name changes) |
| `NodeSuiteTests.cs` | **Medium** — integration tests using node definitions | Update to new node creation patterns |
| `NodeViewModelTests.cs` | **None** — ViewModel layer unchanged | No changes |
| `NodeComponentRenderTests.cs` | **None** — UI rendering | No changes |
| `ConnectionPathRenderTests.cs` | **None** — UI rendering | No changes |
| `SocketComponentEditorTests.cs` | **None** — socket editors | No changes |
| `SocketTypeResolverTests.cs` | **Low** — remove `ExecutionPath` type test | Minor update |
| `SocketValueTests.cs` | **None** — value serialization | No changes |
| `PrimitiveModelTests.cs` | **None** — model record tests | No changes |
| `Rect2DTests.cs` | **None** — geometry | No changes |
| `SerializableListVariableTests.cs` | **None** — list model | No changes |
| `GraphSerializerTests.cs` | **Low** — may reference old DefinitionId format | Update DefinitionIds |
| `NodeEditorStateTests.cs` | **Low** — state management | Update `ApplyExecutionContext` call |
| `NodeEditorStateBridgeTests.cs` | **None** — bridge tests | No changes |
| `StateEventTests.cs` | **None** — editor-level events | No changes |
| `SelectionSyncTests.cs` | **None** — selection | No changes |
| `ViewportCullerTests.cs` | **None** — viewport culling | No changes |
| `ModelUiIsolationTests.cs` | **None** — isolation tests | No changes |
| `EditorComponentTests.cs` | **None** — editor UI | No changes |
| `EditorRegistryTests.cs` | **None** — editor registry | No changes |
| `CanvasInteractionHandlerTests.cs` | **None** — interaction handler | No changes |
| `PluginLoaderTests.cs` | **Medium** — tests plugin loading pipeline | Update: remove context registration tests |
| `PluginLifecycleTests.cs` | **Low** — lifecycle hooks | Minor — remove `INodeContext` assertions |
| `PluginEventBusTests.cs` | **None** — event bus | No changes |
| `PluginServiceRegistryTests.cs` | **None** — DI scopes | No changes |
| `DynamicPluginLoadingTests.cs` | **Medium** — loads test plugins | Update: plugins now expose `NodeBase` subclasses |
| `McpAbilityTests.cs` | **Low** — MCP integration | May need DefinitionId updates |
| `McpApiKeyServiceTests.cs` | **None** — key management | No changes |

**Summary**: ~6 files need full/heavy rewrites, ~6 need minor updates, ~18 are unchanged.

---

## 8.2 Execution engine test rewrite

**File**: `NodeEditor.Blazor.Tests/ExecutionEngineTests.cs`

This is the largest test rewrite. Tests should verify:

### 8.2.1 Basic execution flow

```csharp
[Fact]
public async Task StartNode_TriggersExitPath()
{
    // Arrange: Start → DebugPrint (connected via execution)
    var start = CreateNode<StartNode>();
    var debug = CreateNode<DebugPrintNode>();
    var connection = ConnectExecution(start, "Exit", debug, "Enter");

    // Act
    await ExecuteGraphAsync(new[] { start, debug }, new[] { connection });

    // Assert: debug node executed
    AssertNodeExecuted(debug);
}
```

### 8.2.2 Branch execution

```csharp
[Fact]
public async Task BranchNode_TrueCondition_ExecutesTruePath()
{
    // Arrange: Start → Branch → (True: DebugA, False: DebugB)
    var start = CreateNode<StartNode>();
    var branch = CreateNode<BranchNode>();
    var debugTrue = CreateNode<DebugPrintNode>();
    var debugFalse = CreateNode<DebugPrintNode>();

    var connections = new[]
    {
        ConnectExecution(start, "Exit", branch, "Start"),
        ConnectData(/* true constant */, branch, "Cond"),
        ConnectExecution(branch, "True", debugTrue, "Enter"),
        ConnectExecution(branch, "False", debugFalse, "Enter"),
    };

    // Act
    await ExecuteGraphAsync(...);

    // Assert
    AssertNodeExecuted(debugTrue);
    AssertNodeNotExecuted(debugFalse);
}
```

### 8.2.3 For loop execution

```csharp
[Fact]
public async Task ForLoopNode_ExecutesBodyNTimes()
{
    // Arrange: Start → ForLoop(3) → DebugPrint → (back to loop)
    //                  ForLoop.Exit → EndMarker
    var executionCount = 0;

    // Assert: debug node executed exactly 3 times
    // (Track via feedback messages or a custom counting mechanism)
}
```

### 8.2.4 Data-only node lazy execution

```csharp
[Fact]
public async Task DataNode_ExecutedLazily_WhenDownstreamReads()
{
    // Arrange: Abs(Value: -5) → DebugPrint(Value: connected to Abs.Result)
    // Abs is not callable — should execute lazily when DebugPrint reads its input

    // Assert: Abs node executed, result = 5
}
```

### 8.2.5 Streaming execution

```csharp
[Fact]
public async Task StreamingNode_EmitsItemsSequentially()
{
    // Arrange: a test streaming node that emits 3 items
    //          OnToken → CollectorNode (captures each item)
    //          Completed → FinalNode

    // Assert: collector received items in order
    // Assert: FinalNode executed after all items
}

[Fact]
public async Task StreamingNode_CompletedPath_FiresAfterAllItems()
{
    // Assert ordering: OnToken(1) → OnToken(2) → OnToken(3) → Completed
}
```

### 8.2.6 Nested loops

```csharp
[Fact]
public async Task NestedForLoops_ExecuteCorrectly()
{
    // Outer loop 3x → Inner loop 2x → Debug
    // Assert: Debug executed 6 times total
}
```

### 8.2.7 Variable get/set

```csharp
[Fact]
public async Task VariableSetAndGet_SharesValue()
{
    // Start → SetVariable("x", 42) → GetVariable("x") → Debug(Value: x)
    // Assert: Debug prints 42
}
```

### 8.2.8 Cancellation

```csharp
[Fact]
public async Task Cancellation_StopsExecution()
{
    // Start → InfiniteLoop (While true) → Debug
    // Cancel after 100ms
    // Assert: OperationCanceledException thrown
}
```

### 8.2.9 Execution gate (step debug)

```csharp
[Fact]
public async Task ExecutionGate_PausesExecution()
{
    // Start paused → StepOnce() → verify one node executed → Resume()
}
```

---

## 8.3 New: Streaming-specific test file

**File**: `NodeEditor.Blazor.Tests/StreamingExecutionTests.cs` (new)

```csharp
/// <summary>
/// Tests for the streaming execution system (EmitAsync, OnItem path, Completed path).
/// </summary>
public class StreamingExecutionTests
{
    // ── Sequential mode ──

    [Fact]
    public async Task EmitAsync_Sequential_WaitsForDownstream()
    {
        // Streaming node emits 3 items
        // Each item triggers downstream node that takes 50ms
        // Assert: total time ≥ 150ms (sequential)
    }

    [Fact]
    public async Task EmitAsync_Sequential_ItemsReceivedInOrder()
    {
        // Emit items 1, 2, 3
        // Downstream captures them
        // Assert: received in order [1, 2, 3]
    }

    // ── FireAndForget mode ──

    [Fact]
    public async Task EmitAsync_FireAndForget_DoesNotWaitForDownstream()
    {
        // Streaming node emits 3 items with fire-and-forget
        // Each downstream takes 100ms
        // Assert: total time < 300ms (concurrent)
    }

    // ── Completed path ──

    [Fact]
    public async Task CompletedPath_FiresAfterAllItems()
    {
        // Stream 3 items → Completed should fire once after all OnItem
    }

    [Fact]
    public async Task CompletedPath_DownstreamHasAccessToFinalState()
    {
        // Stream + accumulate → Completed path reads final accumulated value
    }

    // ── Error handling ──

    [Fact]
    public async Task EmitAsync_DownstreamError_PropagatesInSequential()
    {
        // If downstream throws during OnItem, error propagates to streaming node
    }

    // ── Cancellation ──

    [Fact]
    public async Task EmitAsync_Cancellation_StopsStreaming()
    {
        // Cancel during streaming → node stops emitting
    }
}
```

---

## 8.4 Update `NodeRegistryTests.cs`

### Tests to update
- Discovery tests: verify `NodeBase` subclasses are discovered, not `INodeContext` + `[Node]`
- Registration tests: verify `NodeBuilder`-created definitions are registered correctly
- Catalog tests: unchanged (catalog groups by `Category`)
- Remove/unregister tests: unchanged

```csharp
[Fact]
public void Discovery_FindsNodeBaseSubclasses()
{
    var service = new NodeDiscoveryService();
    var defs = service.DiscoverFromAssemblies(new[] { typeof(StartNode).Assembly });

    Assert.Contains(defs, d => d.Name == "Start");
    Assert.Contains(defs, d => d.Name == "For Loop");
    Assert.Contains(defs, d => d.Name == "Branch");
}

[Fact]
public void NodeBuilder_CreatesValidDefinition()
{
    var def = NodeBuilder.Create("Test")
        .Category("Tests")
        .Input<int>("Value")
        .Output<string>("Result")
        .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<int>("Value").ToString()))
        .Build();

    Assert.Equal("Test", def.Name);
    Assert.Single(def.Inputs);
    Assert.Single(def.Outputs);
    Assert.NotNull(def.InlineExecutor);
    Assert.NotNull(def.Factory);
}
```

---

## 8.5 Update `PluginLoaderTests.cs`

### Tests to update
- Remove any assertion that checks `INodeContextRegistry` was populated
- Update: verify that `NodeBase` subclasses from plugin assemblies are discovered
- Keep: plugin lifecycle tests (load, unload, register, etc.)

---

## 8.6 Update `DynamicPluginLoadingTests.cs`

### Tests to update
- Test plugins now expose `NodeBase` subclasses instead of `INodeContext` types
- Verify: `registry.Definitions` contains definitions from plugin `NodeBase` subclasses after loading
- Verify: definitions removed after unloading

---

## 8.7 Update `GraphSerializerTests.cs`

### Tests to update
- Verify old-format graphs deserialize correctly (DefinitionId migration)
- Verify new-format graphs round-trip correctly
- Add: migration test — old DefinitionId → new DefinitionId mapping

---

## 8.8 Helper infrastructure for tests

### `TestStreamingNode` — reusable test node for streaming tests

```csharp
public sealed class TestStreamingNode : NodeBase
{
    private readonly int _itemCount;
    private readonly int _delayPerItemMs;

    public TestStreamingNode(int itemCount = 3, int delayPerItemMs = 0)
    {
        _itemCount = itemCount;
        _delayPerItemMs = delayPerItemMs;
    }

    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("Test Stream")
            .Category("Test")
            .Callable()
            .StreamOutput<string>("Item", "OnItem", "Completed");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        for (int i = 0; i < _itemCount; i++)
        {
            if (_delayPerItemMs > 0)
                await Task.Delay(_delayPerItemMs, ct);
            await context.EmitAsync("Item", $"item-{i}");
        }
        await context.TriggerAsync("Completed");
    }
}
```

### `TestCollectorNode` — captures received items for assertions

```csharp
public sealed class TestCollectorNode : NodeBase
{
    public List<object?> Collected { get; } = new();

    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Collector").Category("Test").Callable().Input<object>("Value");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        Collected.Add(context.GetInput<object>("Value"));
        return context.TriggerAsync("Exit");
    }
}
```

### Graph builder helper — fluent test graph construction

```csharp
public sealed class TestGraphBuilder
{
    private readonly List<NodeData> _nodes = new();
    private readonly List<ConnectionData> _connections = new();

    public TestGraphBuilder AddNode(NodeBase node, out string nodeId)
    {
        var builder = NodeBuilder.CreateForType(node.GetType());
        node.Configure(builder);
        var def = builder.Build();
        var data = def.Factory();
        nodeId = data.Id;
        _nodes.Add(data);
        return this;
    }

    public TestGraphBuilder ConnectExecution(string fromNodeId, string fromSocket, string toNodeId, string toSocket)
    {
        _connections.Add(new ConnectionData(fromNodeId, toNodeId, fromSocket, toSocket, true));
        return this;
    }

    public TestGraphBuilder ConnectData(string fromNodeId, string fromSocket, string toNodeId, string toSocket)
    {
        _connections.Add(new ConnectionData(fromNodeId, toNodeId, fromSocket, toSocket, false));
        return this;
    }

    public (IReadOnlyList<NodeData> Nodes, IReadOnlyList<ConnectionData> Connections) Build()
        => (_nodes.AsReadOnly(), _connections.AsReadOnly());
}
```

---

## 8.9 Verification checklist

After all tests are updated:

- [ ] `dotnet build NodeEditor.slnx` — compiles clean, no references to removed types
- [ ] `dotnet test NodeEditor.Blazor.Tests/NodeEditor.Blazor.Tests.csproj` — all tests pass
- [ ] Test coverage: all standard node types have at least one execution test
- [ ] Test coverage: streaming OnItem + Completed paths verified
- [ ] Test coverage: loop nodes (For, ForStep, ForEach, While, DoWhile, RepeatUntil) verified
- [ ] Test coverage: branch node verified
- [ ] Test coverage: data-only node lazy execution verified
- [ ] Test coverage: variable get/set verified
- [ ] Test coverage: cancellation verified
- [ ] Test coverage: old graph deserialization (DefinitionId migration) verified
- [ ] Test coverage: plugin loading with `NodeBase` subclasses verified

---

## Files impacted by this phase

| Action | File | Notes |
|--------|------|-------|
| **Rewrite** | `NodeEditor.Blazor.Tests/ExecutionEngineTests.cs` | Complete rewrite for coroutine model |
| **Create** | `NodeEditor.Blazor.Tests/StreamingExecutionTests.cs` | New: streaming-specific tests |
| **Modify** | `NodeEditor.Blazor.Tests/NodeRegistryTests.cs` | Update discovery tests |
| **Modify** | `NodeEditor.Blazor.Tests/PluginLoaderTests.cs` | Remove context registry assertions |
| **Modify** | `NodeEditor.Blazor.Tests/DynamicPluginLoadingTests.cs` | Update for `NodeBase` plugins |
| **Modify** | `NodeEditor.Blazor.Tests/GraphSerializerTests.cs` | Add migration tests |
| **Modify** | `NodeEditor.Blazor.Tests/SocketTypeResolverTests.cs` | Remove `ExecutionPath` test |
| **Modify** | `NodeEditor.Blazor.Tests/NodeEditorStateTests.cs` | `ApplyExecutionContext` signature |
| **Minor/None** | 18 other test files | Unchanged or trivial updates |

## Dependencies

- Depends on all previous phases (1–7) being complete
- Tests are the final verification that everything works end-to-end
