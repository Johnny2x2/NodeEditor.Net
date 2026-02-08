using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Services.Registry;

public sealed record class NodeDefinition(
    string Id,
    string Name,
    string Category,
    string Description,
    IReadOnlyList<SocketData> Inputs,
    IReadOnlyList<SocketData> Outputs,
    Func<NodeData> Factory);
