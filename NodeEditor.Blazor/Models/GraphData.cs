namespace NodeEditor.Blazor.Models;

/// <summary>
/// Pure data representation of a complete node graph.
/// Supports both UI rendering (with layout) and headless execution.
/// </summary>
public sealed record class GraphData(
    IReadOnlyList<GraphNodeData> Nodes,
    IReadOnlyList<ConnectionData> Connections,
    IReadOnlyList<GraphVariable> Variables,
    int SchemaVersion = 1);

/// <summary>
/// A node with both its domain data and spatial layout.
/// Combines NodeData (execution) + position/size (rendering/serialization).
/// </summary>
public sealed record class GraphNodeData(
    NodeData Data,
    Point2D Position,
    Size2D Size);
