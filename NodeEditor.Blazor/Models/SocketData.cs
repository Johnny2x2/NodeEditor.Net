namespace NodeEditor.Blazor.Models;

public sealed record class SocketData(
    string Name,
    string TypeName,
    bool IsInput,
    bool IsExecution,
    SocketValue? Value = null,
    SocketEditorHint? EditorHint = null
);
