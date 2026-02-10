# 4G — List Inline Nodes

> **Parallelism**: Can run in parallel with **4A**, **4B**, **4C**, **4D**, **4E**, **4F**.

## Prerequisites
- **Phase 1** complete (`NodeBuilder`, `INodeExecutionContext`)

## Can run in parallel with
- All other Phase 4 sub-tasks

## Deliverable

### `StandardListNodes` — 12 inline lambda definitions

**File**: `NodeEditor.Net/Services/Execution/StandardNodes/StandardListNodes.cs`

Full code: see original [04-phase4-standard-nodes.md](../04-phase4-standard-nodes.md) section 4.4 — `StandardListNodes`.

**Nodes**: List Create, List Add, List Insert, List Remove At, List Remove Value, List Clear, List Contains, List Index Of, List Count, List Get, List Set, List Slice.

All use `SerializableList` for list values and clone-on-write for immutability.

## Acceptance criteria

- [ ] `GetDefinitions()` returns 12 `NodeDefinition` instances
- [ ] All nodes in "Lists" category
- [ ] Create returns empty list, Add appends item, Insert at index works
- [ ] Remove At, Remove Value, Clear produce correct results
- [ ] Contains, IndexOf, Count return correct values
- [ ] Get retrieves by index, Set replaces at index, Slice returns sub-list
- [ ] All operations use clone-on-write (don't mutate input list)
