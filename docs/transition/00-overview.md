# Node System Transition Plan — Overview

## Goal

Replace the **attribute + delegate + reflection** node system with a **class-based node + fluent builder** architecture. Every node becomes either a `NodeBase` subclass (for stateful/control-flow nodes) or an inline lambda registration via `NodeBuilder` (for pure-function data nodes). The engine dispatches directly to `ExecuteAsync()` — no more reflection-based `NodeMethodInvoker`.

## What changes

| Layer | Old system | New system |
|-------|-----------|------------|
| **Node definition** | `INodeContext` class + `[Node]` attribute on methods | `NodeBase` subclass with `Configure(NodeBuilder)` + `ExecuteAsync()`, or `NodeBuilder` standalone |
| **Socket definition** | C# method parameters (normal → input, `out` → output) | `NodeBuilder` fluent API: `.Input<T>()`, `.Output<T>()`, `.ExecutionInput()`, `.ExecutionOutput()` |
| **Discovery** | Scan assemblies for `INodeContext` types, reflect over `[Node]` methods | Scan assemblies for `NodeBase` subclasses, call `Configure()` |
| **Execution dispatch** | `NodeMethodInvoker` resolves method via reflection, marshals parameters | Direct call to `nodeInstance.ExecuteAsync(context, ct)` |
| **Flow control** | Engine-driven: `ExecutionPath.Signal()`, planner builds `LoopStep`/`BranchStep`, engine re-invokes methods | Node-driven: `context.TriggerAsync("Exit")`, loops are real `for` loops inside `ExecuteAsync()` |
| **Per-node state** | Shared `ConcurrentDictionary<string, object>` on `StandardNodeContext`, key-hacked by node ID | Instance-per-node: each canvas node gets its own `NodeBase` instance, state is normal class fields |
| **Streaming** | Not supported | Built-in: `context.EmitAsync<T>(socketName, item)` yields items to downstream execution paths |
| **DI** | `CompositeNodeContext` aggregation, no real DI into node methods | `context.Services` (`IServiceProvider`) available during execution; `OnCreatedAsync(IServiceProvider)` for setup |

## What stays the same

- **`NodeDefinition`** record — extended with new fields, but the existing `(Id, Name, Category, Description, Inputs, Outputs, Factory)` shape is preserved
- **`NodeData`**, **`SocketData`**, **`ConnectionData`** immutable models — unchanged
- **`NodeViewModel`**, **`SocketViewModel`** — unchanged
- **UI components** — `NodeEditorCanvas.razor`, `NodeComponent.razor`, `ConnectionPath.razor` work off `NodeDefinition.Factory()` → `NodeData` → `NodeViewModel`, no changes needed
- **Serialization** — `GraphSerializer` uses `DefinitionId` strings, round-trip preserved
- **`VariableNodeFactory`** / **`EventNodeFactory`** — already build `NodeDefinition`s programmatically, will be updated to use `NodeBuilder`
- **MCP integration** — `NodeAbilityProvider` uses `definition.Factory()`, no change needed

## Phase ordering

| Phase | Scope | Files affected |
|-------|-------|---------------|
| [Phase 1](./01-phase1-core-abstractions.md) | `NodeBase`, `NodeBuilder`, new `INodeExecutionContext` | ~5 new files in `NodeEditor.Net/Services/Execution/Nodes/` and `Context/` |
| [Phase 2](./02-phase2-discovery-registry.md) | Discovery rewrite, `NodeDefinition` extension, registry updates | ~3 files modified in `NodeEditor.Net/Services/Registry/` |
| [Phase 3](./03-phase3-execution-engine.md) | Execution engine rewrite (coroutine model), planner simplification | ~4 files in `NodeEditor.Net/Services/Execution/Runtime/` and `Planning/` |
| [Phase 4](./04-phase4-standard-nodes.md) | Migrate all ~40 standard nodes to `NodeBase` / `NodeBuilder` | ~20 new files, ~8 old files deleted in `StandardNodes/` |
| [Phase 5](./05-phase5-remove-old-infra.md) | Remove `INodeContext`, `NodeAttribute`, `NodeMethodInvoker`, `ExecutionPath`, etc. | ~10 files deleted |
| [Phase 6](./06-phase6-plugin-system.md) | Update plugin contracts, loader, templates | ~6 files across `Plugins/` and plugin projects |
| [Phase 7](./07-phase7-blazor-integration.md) | DI registration updates, headless runner, serializer | ~4 files in `NodeEditor.Blazor/` and `NodeEditor.Net/` |
| [Phase 8](./08-phase8-tests.md) | Rewrite execution tests, add streaming tests, update all affected tests | ~15 test files |

## Key design decisions

1. **Coroutine model over plan-driven**: Flow control lives inside node code. `TriggerAsync()` suspends the node, runs the downstream execution path, then resumes. The planner reduces to graph validation only.
2. **Instance-per-node**: Each canvas node gets its own `NodeBase` during execution. No shared state dictionary hacking.
3. **Builder-only sockets**: All sockets declared via `NodeBuilder` fluent API. No attribute-based socket definition.
4. **DI via context**: `context.Services` provides `IServiceProvider`. Node classes can also override `OnCreatedAsync(IServiceProvider)` for one-time setup.
5. **Data nodes as inline lambdas**: ~35 pure-function nodes (math, string, list) use `NodeBuilder.Create().OnExecute(lambda)` — no subclassing needed.
6. **Streaming built-in**: `EmitAsync<T>()` + `OnItem`/`Completed` execution paths are first-class. Configurable blocking mode (sequential vs fire-and-forget).
7. **Complete replacement**: Old attribute system is removed entirely, not deprecated alongside.
