namespace NodeEditor.Blazor.Models;

public sealed record class NodeData(
    string Id,
    string Name,
    bool Callable,
    bool ExecInit,
    IReadOnlyList<SocketData> Inputs,
    IReadOnlyList<SocketData> Outputs,
    string? DefinitionId = null
);
