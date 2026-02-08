using NodeEditor.Net.Models;

namespace NodeEditor.Net.Services.Execution;

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class SocketEditorAttribute : Attribute
{
    public SocketEditorAttribute(SocketEditorKind kind)
    {
        Kind = kind;
    }

    public SocketEditorKind Kind { get; }

    public string? Options { get; init; }

    public double Min { get; init; } = double.NaN;

    public double Max { get; init; } = double.NaN;

    public double Step { get; init; } = double.NaN;

    public string? Placeholder { get; init; }

    public string? Label { get; init; }
}
