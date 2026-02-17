# 7G — List Inline Nodes

> **Phase 7 — Standard Nodes**
> All sub-tasks in this phase can run fully in parallel.

## Prerequisites
- **Phases 1–3** complete (`NodeBuilder`, `INodeExecutionContext`, `NodeDefinition` extended)

## Can run in parallel with
- **7A**, **7B**, **7C**, **7D**, **7E**, **7F**

## Deliverable

### `StandardListNodes` — 12 inline lambda definitions

**File**: `NodeEditor.Net/Services/Execution/StandardNodes/StandardListNodes.cs`

Full code for `StandardListNodes` is detailed below.

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
