# 12B — Registry & Discovery Tests

> **Phase 12 — Independent Tests**
> All sub-tasks in this phase can run fully in parallel.

## Prerequisites
- **Phases 1–4** complete (NodeBase, NodeBuilder, NodeDiscoveryService)

## Can run in parallel with
- All other Phase 12 sub-tasks

## Deliverable

### Update `NodeRegistryTests.cs`

**File**: `NodeEditor.Blazor.Tests/NodeRegistryTests.cs`

**Tests to rewrite**:
- Discovery tests → verify `NodeBase` subclasses found, not `INodeContext`+`[Node]`
- Registration tests → verify `NodeBuilder`-created definitions register correctly
- Catalog tests → unchanged (groups by Category)
- Remove/unregister tests → unchanged

**New test cases**:

```csharp
[Fact]
public void Discovery_FindsNodeBaseSubclasses()
{
    var service = new NodeDiscoveryService();
    var defs = service.DiscoverFromAssemblies(new[] { typeof(StartNode).Assembly });
    Assert.Contains(defs, d => d.Name == "Start");
    Assert.Contains(defs, d => d.Name == "For Loop");
    Assert.Contains(defs, d => d.Name == "Branch");
}

[Fact]
public void NodeBuilder_CreatesValidDefinition()
{
    var def = NodeBuilder.Create("Test")
        .Category("Tests").Input<int>("Value").Output<string>("Result")
        .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<int>("Value").ToString()))
        .Build();
    Assert.Equal("Test", def.Name);
    Assert.Single(def.Inputs);
    Assert.Single(def.Outputs);
    Assert.NotNull(def.InlineExecutor);
}
```

## Acceptance criteria

- [ ] Discovery finds all `NodeBase` subclasses
- [ ] `NodeBuilder.Build()` produces valid definitions with all fields
- [ ] Catalog grouping works with new definitions
- [ ] Registration/unregistration still works
