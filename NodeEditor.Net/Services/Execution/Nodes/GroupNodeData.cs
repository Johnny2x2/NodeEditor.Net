using NodeEditor.Net.Models;

namespace NodeEditor.Net.Services.Execution;

public sealed record GroupSocketMapping(string GroupSocketName, string NodeId, string SocketName);

public sealed record GroupNodeData(
    string Id,
    string Name,
    IReadOnlyList<NodeData> Nodes,
    IReadOnlyList<ConnectionData> Connections,
    IReadOnlyList<SocketData> Inputs,
    IReadOnlyList<SocketData> Outputs,
    IReadOnlyList<GroupSocketMapping> InputMappings,
    IReadOnlyList<GroupSocketMapping> OutputMappings);
