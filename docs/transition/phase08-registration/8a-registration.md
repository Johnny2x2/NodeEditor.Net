# 8A — Registration Aggregator

> **Phase 8 — Registration Aggregator**
> Single sub-task phase. Depends on Phase 7 completing first.

## Prerequisites
- **7E** (StandardNumberNodes)
- **7F** (StandardStringNodes)
- **7G** (StandardListNodes)

## Can run in parallel with
- Nothing (sole sub-task in this phase). Very small — < 15 minutes.

## Deliverable

### `StandardNodeRegistration` — Aggregator

**File**: `NodeEditor.Net/Services/Execution/StandardNodes/StandardNodeRegistration.cs`

```csharp
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution.StandardNodes;

/// <summary>
/// Collects all inline (lambda) standard node definitions.
/// Called during NodeRegistryService initialization.
/// </summary>
public static class StandardNodeRegistration
{
    public static IEnumerable<NodeDefinition> GetInlineDefinitions()
    {
        foreach (var def in StandardNumberNodes.GetDefinitions()) yield return def;
        foreach (var def in StandardStringNodes.GetDefinitions()) yield return def;
        foreach (var def in StandardListNodes.GetDefinitions()) yield return def;
    }
}
```

The `NodeRegistryService.EnsureInitialized()` (updated in 4B) calls this to register all data nodes.

## Acceptance criteria

- [ ] `GetInlineDefinitions()` returns ~34 definitions (10 + 12 + 12)
- [ ] All definitions have unique `Id` values
- [ ] Can be called by `NodeRegistryService.RegisterDefinitions()` successfully
