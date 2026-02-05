namespace NodeEditor.Blazor.Models;

public sealed record class SocketEditorHint(
    SocketEditorKind Kind,
    string? Options = null,
    double? Min = null,
    double? Max = null,
    double? Step = null,
    string? Placeholder = null,
    string? Label = null);
