# 7B — DefinitionId Migration & GraphSerializer

> **Parallelism**: Can run in parallel with **7A**, **7C**, **7D**, **7E**.

## Prerequisites
- **Phase 4** complete (need new DefinitionIds to build the mapping)

## Can run in parallel with
- All other Phase 7 sub-tasks

## Deliverables

### Create `DefinitionIdMigration`

**File**: `NodeEditor.Net/Services/Serialization/DefinitionIdMigration.cs` (new)

```csharp
public static class DefinitionIdMigration
{
    private static readonly Dictionary<string, string> _map = new(StringComparer.Ordinal)
    {
        // Helpers
        ["NodeEditor.Net.Services.Execution.StandardNodeContext.Start(NodeEditor.Net.Services.Execution.ExecutionPath&)"]
            = "NodeEditor.Net.Services.Execution.StandardNodes.StartNode",
        ["NodeEditor.Net.Services.Execution.StandardNodeContext.Marker(NodeEditor.Net.Services.Execution.ExecutionPath,NodeEditor.Net.Services.Execution.ExecutionPath&)"]
            = "NodeEditor.Net.Services.Execution.StandardNodes.MarkerNode",

        // Conditions
        ["NodeEditor.Net.Services.Execution.StandardNodeContext.Branch(NodeEditor.Net.Services.Execution.ExecutionPath,System.Boolean,NodeEditor.Net.Services.Execution.ExecutionPath&,NodeEditor.Net.Services.Execution.ExecutionPath&)"]
            = "NodeEditor.Net.Services.Execution.StandardNodes.BranchNode",
        ["NodeEditor.Net.Services.Execution.StandardNodeContext.ForLoop(System.Int32,NodeEditor.Net.Services.Execution.ExecutionPath&,NodeEditor.Net.Services.Execution.ExecutionPath&,System.Int32&)"]
            = "NodeEditor.Net.Services.Execution.StandardNodes.ForLoopNode",

        // ... all other standard nodes ...

        // Data nodes (old method signature → new name-based)
        // e.g.: "NodeEditor.Net.Services.Execution.StandardNodeContext.Abs(System.Double)" → "Abs"
    };

    public static string Migrate(string definitionId)
        => _map.TryGetValue(definitionId, out var newId) ? newId : definitionId;
}
```

### Update `GraphSerializer`

**File**: `NodeEditor.Net/Services/Serialization/GraphSerializer.cs`

During deserialization, apply migration to each node's `DefinitionId`:

```csharp
// In deserialize method, after parsing NodeData:
var migratedId = DefinitionIdMigration.Migrate(nodeData.DefinitionId);
// Use migratedId for definition lookup
```

## Acceptance criteria

- [ ] `DefinitionIdMigration` has mappings for all ~50 standard nodes
- [ ] `Migrate()` returns new ID for old-format strings
- [ ] `Migrate()` returns input unchanged for already-new-format strings
- [ ] `GraphSerializer` applies migration during deserialization
- [ ] Old saved graphs deserialize and nodes resolve to correct definitions
- [ ] New saved graphs round-trip correctly
