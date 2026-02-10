# 8F — Serialization & Migration Tests

> **Parallelism**: Can run in parallel with **8A**, **8B**, **8C**, **8D**, **8E**, **8G**.

## Prerequisites
- **7B** complete (`DefinitionIdMigration` exists)

## Can run in parallel with
- All other Phase 8 sub-tasks

## Deliverables

### Update `GraphSerializerTests.cs`

- Verify old-format graphs deserialize correctly (DefinitionId migration applied)
- Verify new-format graphs round-trip correctly
- Add migration-specific test

**New test cases**:

```csharp
[Fact]
public void DefinitionIdMigration_MapsOldToNew()
{
    var oldId = "NodeEditor.Net.Services.Execution.StandardNodeContext.Start(NodeEditor.Net.Services.Execution.ExecutionPath&)";
    var newId = DefinitionIdMigration.Migrate(oldId);
    Assert.Equal("NodeEditor.Net.Services.Execution.StandardNodes.StartNode", newId);
}

[Fact]
public void DefinitionIdMigration_PassesThroughUnknown()
{
    var unknownId = "SomePlugin.CustomNode";
    Assert.Equal(unknownId, DefinitionIdMigration.Migrate(unknownId));
}

[Fact]
public void Deserialize_OldGraph_MigratesDefinitionIds()
{
    // Load a saved graph JSON with old-style DefinitionIds
    // Deserialize → verify nodes resolve to correct new definitions
}
```

## Acceptance criteria

- [ ] Old DefinitionIds migrate to new format
- [ ] Unknown DefinitionIds pass through unchanged
- [ ] Old saved graphs load and nodes resolve correctly
- [ ] New graphs round-trip without data loss
