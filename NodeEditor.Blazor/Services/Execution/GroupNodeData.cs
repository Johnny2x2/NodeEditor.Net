using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Services.Execution;

public sealed record GroupNodeData(
    string Id,
    string Name,
    IReadOnlyList<NodeData> Nodes,
    IReadOnlyList<ConnectionData> Connections,
    IReadOnlyList<SocketData> Inputs,
    IReadOnlyList<SocketData> Outputs);
