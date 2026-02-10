# 8G — Minor Test Updates

> **Parallelism**: Can run in parallel with **8A**, **8B**, **8C**, **8D**, **8E**, **8F**.

## Prerequisites
- **Phases 1, 5** complete (type renames applied)

## Can run in parallel with
- All other Phase 8 sub-tasks

## Deliverables

### `SocketTypeResolverTests.cs`
- Remove test for `ExecutionPath` type registration
- Verify other type registrations unchanged

### `NodeEditorStateTests.cs`
- Update `ApplyExecutionContext` call to use `INodeRuntimeStorage` parameter type

### `NodeAdapterTests.cs`
- Minor: update any type name strings if they reference old types

### `NodeSuiteTests.cs`
- Update integration tests to use new node creation patterns (if they create nodes directly)

### `McpAbilityTests.cs`
- May need DefinitionId updates if tests check specific node IDs

### Files with NO changes needed (verify they still compile)
- `NodeViewModelTests.cs`, `NodeComponentRenderTests.cs`, `ConnectionPathRenderTests.cs`
- `SocketComponentEditorTests.cs`, `SocketValueTests.cs`, `PrimitiveModelTests.cs`
- `Rect2DTests.cs`, `SerializableListVariableTests.cs`, `NodeEditorStateBridgeTests.cs`
- `StateEventTests.cs`, `SelectionSyncTests.cs`, `ViewportCullerTests.cs`
- `ModelUiIsolationTests.cs`, `EditorComponentTests.cs`, `EditorRegistryTests.cs`
- `CanvasInteractionHandlerTests.cs`, `McpApiKeyServiceTests.cs`

## Acceptance criteria

- [ ] `SocketTypeResolverTests` — no `ExecutionPath` test, other tests pass
- [ ] `NodeEditorStateTests` — compiles with `INodeRuntimeStorage` parameter
- [ ] All unchanged test files still compile and pass
- [ ] `dotnet test NodeEditor.Blazor.Tests/NodeEditor.Blazor.Tests.csproj` — all tests green
