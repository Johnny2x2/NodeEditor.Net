# 13A — Execution Engine Tests Rewrite

> **Phase 13 — Dependent Tests**
> All sub-tasks in this phase can run in parallel with each other.

## Prerequisites
- **12A** (Test Infrastructure)
- **All implementation phases** (1–11) complete

## Can run in parallel with
- **13B**

## Deliverable

### Full rewrite of `ExecutionEngineTests.cs`

**File**: `NodeEditor.Blazor.Tests/ExecutionEngineTests.cs`

**Test cases**:

| Test | What it verifies |
|------|------------------|
| `StartNode_TriggersExitPath` | Basic execution chain |
| `BranchNode_TrueCondition_ExecutesTruePath` | Conditional branching |
| `BranchNode_FalseCondition_ExecutesFalsePath` | Conditional branching (other path) |
| `ForLoopNode_ExecutesBodyNTimes` | Loop iteration count |
| `ForLoopStepNode_HandlesNegativeStep` | Step direction |
| `ForEachLoopNode_IteratesItems` | List iteration |
| `WhileLoopNode_StopsWhenConditionFalse` | While termination |
| `DoWhileLoopNode_ExecutesAtLeastOnce` | Do-while semantics |
| `RepeatUntilNode_StopsWhenConditionTrue` | Repeat-until semantics |
| `NestedForLoops_ExecuteCorrectly` | 3x2 = 6 iterations |
| `DataNode_ExecutedLazily_WhenDownstreamReads` | Lazy eval for data-only |
| `VariableSetAndGet_SharesValue` | Graph variable passing |
| `Cancellation_StopsExecution` | CancellationToken respect |
| `ExecutionGate_PausesExecution` | Step debugging support |
| `MultipleInitiators_ExecuteInParallel` | Parallel start nodes |

All tests use `TestGraphBuilder` for graph construction and `INodeExecutionService` for execution.

## Acceptance criteria

- [ ] All 15+ test cases pass
- [ ] Every standard control flow node has at least one test
- [ ] Data-only lazy execution verified
- [ ] Cancellation verified
- [ ] Execution gate verified
