# 5A — Delete Old Files

> **Parallelism**: Can run in parallel with **5B** and **5C**.

## Prerequisites
- **Phase 3** complete (execution engine no longer uses old types)
- **Phase 4** complete (all standard nodes migrated)

## Can run in parallel with
- **5B** (Update Factories), **5C** (Reference Cleanup)

## Deliverable

Delete the following 18 files:

### Node definition infrastructure (3 files)
| File | Why |
|------|-----|
| `NodeEditor.Net/Services/Registry/INodeContext.cs` | Replaced by `NodeBase` |
| `NodeEditor.Net/Services/Execution/Nodes/NodeAttribute.cs` | Replaced by `NodeBase.Configure(INodeBuilder)` |
| `NodeEditor.Net/Services/Execution/Nodes/SocketEditorAttribute.cs` | Replaced by `NodeBuilder.Input<T>(..., editorHint)` |

### Execution dispatch infrastructure (2 files)
| File | Why |
|------|-----|
| `NodeEditor.Net/Services/Execution/Runtime/NodeMethodInvoker.cs` | Replaced by direct `NodeBase.ExecuteAsync()` calls |
| `NodeEditor.Net/Services/Execution/Helpers/ExecutionPath.cs` | Replaced by `TriggerAsync()` + `ExecutionSocket` marker |

### Context aggregation infrastructure (5 files)
| File | Why |
|------|-----|
| `NodeEditor.Net/Services/Execution/Context/INodeMethodContext.cs` | Replaced by `INodeExecutionContext.EmitFeedback()` |
| `NodeEditor.Net/Services/Execution/Context/INodeContextHost.cs` | Replaced by instance-per-node model |
| `NodeEditor.Net/Services/Execution/Context/CompositeNodeContext.cs` | No longer needed |
| `NodeEditor.Net/Services/Execution/Context/NodeContextFactory.cs` | Discovery scans for `NodeBase` now |
| `NodeEditor.Net/Services/Execution/Context/NodeContextRegistry.cs` | Plugin context registration removed |

### Old standard node files (8 files)
| File | Why |
|------|-----|
| `StandardNodes/StandardNodeContext.cs` | Replaced by individual `NodeBase` subclasses |
| `StandardNodes/StandardNodeContext.Conditions.cs` | Replaced by 4A + 4B |
| `StandardNodes/StandardNodeContext.Helpers.cs` | Replaced by 4C |
| `StandardNodes/StandardNodeContext.DebugPrint.cs` | Replaced by 4D |
| `StandardNodes/StandardNodeContext.Numbers.cs` | Replaced by 4E |
| `StandardNodes/StandardNodeContext.Strings.cs` | Replaced by 4F |
| `StandardNodes/StandardNodeContext.Lists.cs` | Replaced by 4G |
| `StandardNodes/StandardNodeContext.Parallel.cs` | Empty placeholder |

## Acceptance criteria

- [ ] All 18 files deleted
- [ ] `dotnet build NodeEditor.Net/NodeEditor.Net.csproj` does NOT compile (expected — references need cleanup in 5B/5C)
- [ ] No orphaned `.cs` files from old system remain
