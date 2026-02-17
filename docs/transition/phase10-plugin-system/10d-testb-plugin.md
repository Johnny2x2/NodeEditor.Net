# 10D — TestB Plugin Rewrite

> **Phase 10 — Plugin System**
> All sub-tasks in this phase can run fully in parallel.

## Prerequisites
- **Phases 1 2** complete (`NodeBase`, `INodeBuilder`)

## Can run in parallel with
- **10A** (Plugin Loader), **10B** (Template), **10C** (TestA)

## Deliverable

### Rewrite TestB plugin nodes

Same pattern as 10C — replace any `INodeContext` + `[Node]` methods with `NodeBase` subclasses. Specific nodes depend on TestB's current content (review `NodeEditor.Plugins.TestB/` for exact node list).

**General pattern**:
1. For each `[Node]` method on the context class:
   - If pure function (data-only): create inline `NodeBuilder.Create(...)` definition via `INodeProvider`
   - If callable (has `ExecutionPath` params): create `NodeBase` subclass
2. Delete old `*PluginContext` class
3. `Register()` call in the plugin class stays the same

## Acceptance criteria

- [ ] No references to `INodeContext`, `[Node]`, or `ExecutionPath` in TestB project
- [ ] All nodes replaced with `NodeBase` subclasses or inline definitions
- [ ] Plugin loads and all nodes register correctly
