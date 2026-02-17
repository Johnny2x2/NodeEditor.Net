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

All sub-tasks within a phase can be executed in parallel. Phases must be completed sequentially.

| Phase | Scope | Sub-tasks | Files affected |
|-------|-------|-----------|---------------|
| [Phase 1 — Core Types](./phase01-core-types/) | `ExecutionSocket`, `StreamSocketInfo`, `NodeBase`, `INodeExecutionContext` | 1A, 1B | ~3 new files in `Execution/Nodes/` and `Context/` |
| [Phase 2 — Node Builder](./phase02-node-builder/) | `INodeBuilder`, `NodeBuilder` fluent API | 2A | ~2 new files in `Execution/Nodes/` |
| [Phase 3 — Definition Extension](./phase03-definition-extension/) | Extend `NodeDefinition` record with new fields | 3A | ~1 file modified in `Models/` |
| [Phase 4 — Discovery & Registry](./phase04-discovery-registry/) | Rewrite discovery, update registry for `NodeBase` scanning | 4A, 4B | ~2 files in `Services/Registry/` |
| [Phase 5 — Planner & Runtime](./phase05-planner-runtime/) | Simplify planner, implement `ExecutionRuntime` + `NodeExecutionContextImpl` | 5A, 5B | ~3 files in `Execution/Runtime/` and `Planning/` |
| [Phase 6 — Execution Service](./phase06-execution-service/) | Rewrite `NodeExecutionService`, update utilities | 6A, 6B | ~3 files in `Execution/` |
| [Phase 7 — Standard Nodes](./phase07-standard-nodes/) | Migrate all ~40 standard nodes to `NodeBase` / `NodeBuilder` | 7A–7G | ~20 new files in `StandardNodes/` |
| [Phase 8 — Registration](./phase08-registration/) | `StandardNodeRegistration` aggregator | 8A | ~1 new file |
| [Phase 9 — Remove Old Infra](./phase09-remove-old-infra/) | Delete `INodeContext`, `NodeAttribute`, `NodeMethodInvoker`, `ExecutionPath`, etc. | 9A–9C | ~10 files deleted |
| [Phase 10 — Plugin System](./phase10-plugin-system/) | Update plugin loader, template, TestA, TestB | 10A–10D | ~6 files across plugin projects |
| [Phase 11 — Blazor Integration](./phase11-blazor-integration/) | DI registration, DefinitionId migration, headless runner, state, resolver | 11A–11E | ~5 files in `NodeEditor.Blazor/` |
| [Phase 12 — Independent Tests](./phase12-independent-tests/) | Test infrastructure, registry/plugin/serialization/minor tests | 12A–12E | ~10 test files |
| [Phase 13 — Dependent Tests](./phase13-dependent-tests/) | Execution engine + streaming tests (depend on 12A infra) | 13A, 13B | ~5 test files |

---

## Parallel Execution Map

Each phase contains only sub-tasks that can run fully in parallel. No sub-task within a phase depends on another sub-task in the same phase.

### Phase 1 — Core Types

| Sub-task | Document | Depends on | Parallel with |
|----------|----------|------------|---------------|
| **1A** Utility types | [1a](./phase01-core-types/1a-utility-types.md) | — | 1B |
| **1B** Core interfaces | [1b](./phase01-core-types/1b-core-interfaces.md) | — | 1A |

### Phase 2 — Node Builder

| Sub-task | Document | Depends on | Parallel with |
|----------|----------|------------|---------------|
| **2A** Node builder | [2a](./phase02-node-builder/2a-node-builder.md) | Phase 1 | — |

### Phase 3 — Definition Extension

| Sub-task | Document | Depends on | Parallel with |
|----------|----------|------------|---------------|
| **3A** NodeDefinition extension | [3a](./phase03-definition-extension/3a-node-definition-extension.md) | Phase 2 | — |

### Phase 4 — Discovery & Registry

| Sub-task | Document | Depends on | Parallel with |
|----------|----------|------------|---------------|
| **4A** Discovery service | [4a](./phase04-discovery-registry/4a-discovery-service.md) | Phase 3 | 4B |
| **4B** Registry service | [4b](./phase04-discovery-registry/4b-registry-service.md) | Phase 3 | 4A |

### Phase 5 — Planner & Runtime

| Sub-task | Document | Depends on | Parallel with |
|----------|----------|------------|---------------|
| **5A** Planner simplification | [5a](./phase05-planner-runtime/5a-planner-simplification.md) | Phase 4 | 5B |
| **5B** Runtime & context | [5b](./phase05-planner-runtime/5b-runtime-and-context.md) | Phase 4 | 5A |

### Phase 6 — Execution Service

| Sub-task | Document | Depends on | Parallel with |
|----------|----------|------------|---------------|
| **6A** Execution service | [6a](./phase06-execution-service/6a-execution-service.md) | Phase 5 | 6B |
| **6B** Utility updates | [6b](./phase06-execution-service/6b-utility-updates.md) | Phase 5 | 6A |

### Phase 7 — Standard Nodes

| Sub-task | Document | Depends on | Parallel with |
|----------|----------|------------|---------------|
| **7A** Control flow | [7a](./phase07-standard-nodes/7a-control-flow-nodes.md) | Phase 6 | 7B–7G |
| **7B** Loops | [7b](./phase07-standard-nodes/7b-loop-nodes.md) | Phase 6 | 7A, 7C–7G |
| **7C** Helpers | [7c](./phase07-standard-nodes/7c-helper-nodes.md) | Phase 6 | 7A–7B, 7D–7G |
| **7D** Debug | [7d](./phase07-standard-nodes/7d-debug-nodes.md) | Phase 6 | 7A–7C, 7E–7G |
| **7E** Numbers | [7e](./phase07-standard-nodes/7e-number-nodes.md) | Phase 6 | 7A–7D, 7F–7G |
| **7F** Strings | [7f](./phase07-standard-nodes/7f-string-nodes.md) | Phase 6 | 7A–7E, 7G |
| **7G** Lists | [7g](./phase07-standard-nodes/7g-list-nodes.md) | Phase 6 | 7A–7F |

### Phase 8 — Registration

| Sub-task | Document | Depends on | Parallel with |
|----------|----------|------------|---------------|
| **8A** Registration aggregator | [8a](./phase08-registration/8a-registration.md) | 7E, 7F, 7G | — |

### Phase 9 — Remove Old Infrastructure

| Sub-task | Document | Depends on | Parallel with |
|----------|----------|------------|---------------|
| **9A** Delete old files | [9a](./phase09-remove-old-infra/9a-delete-old-files.md) | Phase 8 | 9B, 9C |
| **9B** Update factories | [9b](./phase09-remove-old-infra/9b-update-factories.md) | Phase 8 | 9A, 9C |
| **9C** Reference cleanup | [9c](./phase09-remove-old-infra/9c-reference-cleanup.md) | Phase 8 | 9A, 9B |

### Phase 10 — Plugin System

| Sub-task | Document | Depends on | Parallel with |
|----------|----------|------------|---------------|
| **10A** Plugin loader | [10a](./phase10-plugin-system/10a-plugin-loader.md) | Phase 9 | 10B–10D |
| **10B** Template plugin | [10b](./phase10-plugin-system/10b-template-plugin.md) | Phase 9 | 10A, 10C–10D |
| **10C** TestA plugin | [10c](./phase10-plugin-system/10c-testa-plugin.md) | Phase 9 | 10A–10B, 10D |
| **10D** TestB plugin | [10d](./phase10-plugin-system/10d-testb-plugin.md) | Phase 9 | 10A–10C |

### Phase 11 — Blazor Integration

| Sub-task | Document | Depends on | Parallel with |
|----------|----------|------------|---------------|
| **11A** DI registration | [11a](./phase11-blazor-integration/11a-di-registration.md) | Phase 9 | 11B–11E |
| **11B** DefinitionId migration | [11b](./phase11-blazor-integration/11b-definition-id-migration.md) | Phase 8 | 11A, 11C–11E |
| **11C** Headless runner | [11c](./phase11-blazor-integration/11c-headless-runner.md) | Phase 6 | 11A–11B, 11D–11E |
| **11D** State update | [11d](./phase11-blazor-integration/11d-state-update.md) | Phase 9 | 11A–11C, 11E |
| **11E** Socket type resolver | [11e](./phase11-blazor-integration/11e-socket-type-resolver.md) | Phase 9 | 11A–11D |

### Phase 12 — Independent Tests

| Sub-task | Document | Depends on | Parallel with |
|----------|----------|------------|---------------|
| **12A** Test infrastructure | [12a](./phase12-independent-tests/12a-test-infrastructure.md) | Phase 1 | 12B–12E |
| **12B** Registry tests | [12b](./phase12-independent-tests/12b-registry-tests.md) | Phase 4 | 12A, 12C–12E |
| **12C** Plugin tests | [12c](./phase12-independent-tests/12c-plugin-tests.md) | Phase 10 | 12A–12B, 12D–12E |
| **12D** Serialization tests | [12d](./phase12-independent-tests/12d-serialization-tests.md) | 11B | 12A–12C, 12E |
| **12E** Minor test updates | [12e](./phase12-independent-tests/12e-minor-test-updates.md) | Phase 9 | 12A–12D |

### Phase 13 — Dependent Tests

| Sub-task | Document | Depends on | Parallel with |
|----------|----------|------------|---------------|
| **13A** Execution engine tests | [13a](./phase13-dependent-tests/13a-execution-engine-tests.md) | 12A | 13B |
| **13B** Streaming tests | [13b](./phase13-dependent-tests/13b-streaming-tests.md) | 12A | 13A |

### Critical path

```
1A ─┐                    ┌─ 7A ─┐
    ├─ 2A ─ 3A ─ 4A/4B ─┤      │
1B ─┘       │            ├─ 7B ─┤
            │            ├─ ... ─┼─ 8A ─ 9A/9B/9C ─┬─ 10A ─┐
            │            └─ 7G ─┘                   ├─ 10B  │
            │                                       ├─ 10C  │
            │                                       └─ 10D  │
            │                                               │
            └── 5A/5B ── 6A/6B ── 11C                      │
                                                            │
                              9A/9B/9C ── 11A/11D/11E       │
                                                            │
                                    8A ── 11B               │
                                                            │
                              12A ──┬── 13A                 │
                                    └── 13B                 │
                                                            │
                                    10 ── 12C               │
                                    11B ── 12D              │
                                    9 ── 12E                │
```

The **critical path** runs through: `1A+1B → 2A → 3A → 4A/4B → 5A/5B → 6A/6B → 7x nodes → 8A → 9A → 10A/11A → 12C/12E → 13A`.

Maximum parallelism is in **Phase 7** (7 independent node groups), **Phase 11** (5 sub-tasks), and **Phase 12** (5 independent test groups).

---

## Key design decisions

1. **Coroutine model over plan-driven**: Flow control lives inside node code. `TriggerAsync()` suspends the node, runs the downstream execution path, then resumes. The planner reduces to graph validation only.
2. **Instance-per-node**: Each canvas node gets its own `NodeBase` during execution. No shared state dictionary hacking.
3. **Builder-only sockets**: All sockets declared via `NodeBuilder` fluent API. No attribute-based socket definition.
4. **DI via context**: `context.Services` provides `IServiceProvider`. Node classes can also override `OnCreatedAsync(IServiceProvider)` for one-time setup.
5. **Data nodes as inline lambdas**: ~35 pure-function nodes (math, string, list) use `NodeBuilder.Create().OnExecute(lambda)` — no subclassing needed.
6. **Streaming built-in**: `EmitAsync<T>()` + `OnItem`/`Completed` execution paths are first-class. Configurable blocking mode (sequential vs fire-and-forget).
7. **Complete replacement**: Old attribute system is removed entirely, not deprecated alongside.
