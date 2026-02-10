namespace NodeEditor.Net.Services.Execution;

/// <summary>
/// Metadata about a streaming socket group declared via StreamOutput().
/// </summary>
public sealed record StreamSocketInfo(
    string ItemDataSocket,
    string OnItemExecSocket,
    string? CompletedExecSocket);
