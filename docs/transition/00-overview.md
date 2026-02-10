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

---

## Parallel Execution Map

Each phase is split into independently implementable/testable sub-tasks. Use the sub-task documents for assignment.

### Phase 1 — Core Abstractions

| Sub-task | Document | Depends on | Parallel with |
|----------|----------|------------|---------------|
| **1A** Utility types | [1a](./phase1-core-abstractions/1a-utility-types.md) | — | 1B |
| **1B** Core interfaces | [1b](./phase1-core-abstractions/1b-core-interfaces.md) | — | 1A |
| **1C** Node builder | [1c](./phase1-core-abstractions/1c-node-builder.md) | 1A, 1B | — |

### Phase 2 — Discovery & Registry

| Sub-task | Document | Depends on | Parallel with |
|----------|----------|------------|---------------|
| **2A** NodeDefinition extension | [2a](./phase2-discovery-registry/2a-node-definition-extension.md) | Phase 1 | — |
| **2B** Discovery service | [2b](./phase2-discovery-registry/2b-discovery-service.md) | 2A | 2C |
| **2C** Registry service | [2c](./phase2-discovery-registry/2c-registry-service.md) | 2A | 2B |

### Phase 3 — Execution Engine

| Sub-task | Document | Depends on | Parallel with |
|----------|----------|------------|---------------|
| **3A** Planner simplification | [3a](./phase3-execution-engine/3a-planner-simplification.md) | — | 3B |
| **3B** Runtime & context | [3b](./phase3-execution-engine/3b-runtime-and-context.md) | Phase 1 | 3A |
| **3C** Execution service | [3c](./phase3-execution-engine/3c-execution-service.md) | 3B | 3D |
| **3D** Utility updates | [3d](./phase3-execution-engine/3d-utility-updates.md) | 3B | 3C |

### Phase 4 — Standard Nodes

| Sub-task | Document | Depends on | Parallel with |
|----------|----------|------------|---------------|
| **4A** Control flow | [4a](./phase4-standard-nodes/4a-control-flow-nodes.md) | Phase 1 | 4B–4G |
| **4B** Loops | [4b](./phase4-standard-nodes/4b-loop-nodes.md) | Phase 1 | 4A, 4C–4G |
| **4C** Helpers | [4c](./phase4-standard-nodes/4c-helper-nodes.md) | Phase 1 | 4A–4B, 4D–4G |
| **4D** Debug | [4d](./phase4-standard-nodes/4d-debug-nodes.md) | Phase 1 | 4A–4C, 4E–4G |
| **4E** Numbers | [4e](./phase4-standard-nodes/4e-number-nodes.md) | Phase 1 | 4A–4D, 4F–4G |
| **4F** Strings | [4f](./phase4-standard-nodes/4f-string-nodes.md) | Phase 1 | 4A–4E, 4G |
| **4G** Lists | [4g](./phase4-standard-nodes/4g-list-nodes.md) | Phase 1 | 4A–4F |
| **4H** Registration | [4h](./phase4-standard-nodes/4h-registration.md) | 4A–4G | — |

### Phase 5 — Remove Old Infrastructure

| Sub-task | Document | Depends on | Parallel with |
|----------|----------|------------|---------------|
| **5A** Delete old files | [5a](./phase5-remove-old-infra/5a-delete-old-files.md) | Phases 1–4 | 5B, 5C |
| **5B** Update factories | [5b](./phase5-remove-old-infra/5b-update-factories.md) | Phases 1–4 | 5A, 5C |
| **5C** Reference cleanup | [5c](./phase5-remove-old-infra/5c-reference-cleanup.md) | Phases 1–4 | 5A, 5B |

### Phase 6 — Plugin System

| Sub-task | Document | Depends on | Parallel with |
|----------|----------|------------|---------------|
| **6A** Plugin loader | [6a](./phase6-plugin-system/6a-plugin-loader.md) | Phase 5 | 6B, 6C, 6D |
| **6B** Template plugin | [6b](./phase6-plugin-system/6b-template-plugin.md) | Phase 1 | 6A, 6C, 6D |
| **6C** TestA plugin | [6c](./phase6-plugin-system/6c-testa-plugin.md) | Phase 1 | 6A, 6B, 6D |
| **6D** TestB plugin | [6d](./phase6-plugin-system/6d-testb-plugin.md) | Phase 1 | 6A, 6B, 6C |

### Phase 7 — Blazor Integration

| Sub-task | Document | Depends on | Parallel with |
|----------|----------|------------|---------------|
| **7A** DI registration | [7a](./phase7-blazor-integration/7a-di-registration.md) | Phase 5 | 7B–7E |
| **7B** DefinitionId migration | [7b](./phase7-blazor-integration/7b-definition-id-migration.md) | Phase 4 | 7A, 7C–7E |
| **7C** Headless runner | [7c](./phase7-blazor-integration/7c-headless-runner.md) | Phase 3 | 7A–7B, 7D–7E |
| **7D** State update | [7d](./phase7-blazor-integration/7d-state-update.md) | Phase 1 | 7A–7C, 7E |
| **7E** Socket type resolver | [7e](./phase7-blazor-integration/7e-socket-type-resolver.md) | Phase 5 | 7A–7D |

### Phase 8 — Tests

| Sub-task | Document | Depends on | Parallel with |
|----------|----------|------------|---------------|
| **8A** Test infrastructure | [8a](./phase8-tests/8a-test-infrastructure.md) | Phase 1 | 8D–8G |
| **8B** Execution engine tests | [8b](./phase8-tests/8b-execution-engine-tests.md) | 8A, Phases 1–7 | 8C–8G |
| **8C** Streaming tests | [8c](./phase8-tests/8c-streaming-tests.md) | 8A, Phases 1–7 | 8B, 8D–8G |
| **8D** Registry tests | [8d](./phase8-tests/8d-registry-tests.md) | Phases 1–2 | 8A–8C, 8E–8G |
| **8E** Plugin tests | [8e](./phase8-tests/8e-plugin-tests.md) | Phase 6 | 8A–8D, 8F–8G |
| **8F** Serialization tests | [8f](./phase8-tests/8f-serialization-tests.md) | 7B | 8A–8E, 8G |
| **8G** Minor test updates | [8g](./phase8-tests/8g-minor-test-updates.md) | Phases 1, 5 | 8A–8F |

### Critical path

```
1A ─┐           ┌─ 4A ─┐
    ├─ 1C ─ 2A ─┤      │
1B ─┘     ╲     ├─ 4B ─┤          ┌─ 6B
           ╲    ├─ ... ─┼─ 4H ─── 5A ──┤
            ╲   └─ 4G ─┘     ‖    ├─ 6C ── 8E
             ╲                ‖    └─ 6D
              ╲          5B ──┼─── 6A
               ╲         ‖   │         ┌─ 8B
         3A ────╲── 3C ──┼───┼── 7A    │
                 ╲   ‖   │   │         │
          3B ──── 3D─┘   │   ├── 7E    │
                          │   │         │
                          ├── 7B ── 8F  │
                          │             │
                     5C ──┘    8A ──────┤
                                        └─ 8C
```

The **critical path** runs through: `1A+1B → 1C → 2A → 4x nodes → 4H → 5A → 6A/7A → 8B`.

Maximum parallelism is in **Phase 4** (7 independent node groups) and **Phase 8** (5 independent test groups).

---

## Key design decisions

1. **Coroutine model over plan-driven**: Flow control lives inside node code. `TriggerAsync()` suspends the node, runs the downstream execution path, then resumes. The planner reduces to graph validation only.
2. **Instance-per-node**: Each canvas node gets its own `NodeBase` during execution. No shared state dictionary hacking.
3. **Builder-only sockets**: All sockets declared via `NodeBuilder` fluent API. No attribute-based socket definition.
4. **DI via context**: `context.Services` provides `IServiceProvider`. Node classes can also override `OnCreatedAsync(IServiceProvider)` for one-time setup.
5. **Data nodes as inline lambdas**: ~35 pure-function nodes (math, string, list) use `NodeBuilder.Create().OnExecute(lambda)` — no subclassing needed.
6. **Streaming built-in**: `EmitAsync<T>()` + `OnItem`/`Completed` execution paths are first-class. Configurable blocking mode (sequential vs fire-and-forget).
7. **Complete replacement**: Old attribute system is removed entirely, not deprecated alongside.
