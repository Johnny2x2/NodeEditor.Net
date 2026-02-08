namespace NodeEditor.Blazor.Models;

public sealed record class LegacyNodeSnapshot(
    string Id,
    string Name,
    bool Callable,
    bool ExecInit,
    IReadOnlyList<LegacySocketSnapshot> Inputs,
    IReadOnlyList<LegacySocketSnapshot> Outputs);

public sealed record class LegacySocketSnapshot(
    string Name,
    string TypeName,
    bool IsInput,
    bool IsExecution,
    SocketValue? Value);
