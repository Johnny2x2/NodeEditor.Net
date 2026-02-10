# 6D — TestB Plugin Rewrite

> **Parallelism**: Can run in parallel with **6A**, **6B**, **6C**.

## Prerequisites
- **Phase 1** complete (`NodeBase`, `INodeBuilder`)

## Can run in parallel with
- **6A** (Plugin Loader), **6B** (Template), **6C** (TestA)

## Deliverable

### Rewrite TestB plugin nodes

Same pattern as 6C — replace any `INodeContext` + `[Node]` methods with `NodeBase` subclasses. Specific nodes depend on TestB's current content (review `NodeEditor.Plugins.TestB/` for exact node list).

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
