namespace NodeEditor.Net.Models;

public sealed record class ConnectionData(
    string OutputNodeId,
    string InputNodeId,
    string OutputSocketName,
    string InputSocketName,
    bool IsExecution
);
