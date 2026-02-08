using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Services.Execution;

/// <summary>
/// A step in a hierarchical execution plan. Steps can be parallel layers,
/// loop regions, or branch points.
/// </summary>
public interface IExecutionStep { }

/// <summary>
/// A batch of independent nodes that can execute in parallel.
/// </summary>
public sealed record LayerStep(IReadOnlyList<NodeData> Nodes) : IExecutionStep;

/// <summary>
/// A loop region: the header node is invoked repeatedly; while it signals
/// <see cref="LoopPathSocket"/>, the <see cref="Body"/> sub-steps execute.
/// When the header signals <see cref="ExitPathSocket"/>, execution continues
/// past the loop.
/// </summary>
public sealed record LoopStep(
    NodeData Header,
    string LoopPathSocket,
    string ExitPathSocket,
    IReadOnlyList<IExecutionStep> Body,
    IReadOnlyList<NodeData> BodyNodes) : IExecutionStep;

/// <summary>
/// A branch point: the condition node is executed, then exactly one of the
/// sub-plans runs based on which <see cref="ExecutionPath"/> was signaled.
/// </summary>
public sealed record BranchStep(
    NodeData ConditionNode,
    IReadOnlyList<(string SocketName, IReadOnlyList<IExecutionStep> Steps)> Branches) : IExecutionStep;

/// <summary>
/// A group of independent steps that can execute concurrently.
/// Used when multiple loops or branches at the same level have no dependencies between them.
/// </summary>
public sealed record ParallelSteps(IReadOnlyList<IExecutionStep> Steps) : IExecutionStep;

/// <summary>
/// A hierarchical execution plan composed of steps.
/// </summary>
public sealed record HierarchicalPlan(IReadOnlyList<IExecutionStep> Steps);
