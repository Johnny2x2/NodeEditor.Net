namespace NodeEditor.Blazor.Models;

public sealed record class ConnectionData(
    string OutputNodeId,
    string InputNodeId,
    string OutputSocketName,
    string InputSocketName,
    bool IsExecution
);
