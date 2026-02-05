using Microsoft.AspNetCore.Components;
using NodeEditor.Blazor.Components.Editors;
using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Services.Editors;

public sealed class NumericEditorDefinition : INodeCustomEditor
{
    private static readonly HashSet<string> NumericTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "byte",
        "sbyte",
        "short",
        "ushort",
        "int",
        "int32",
        "uint",
        "uint32",
        "long",
        "int64",
        "ulong",
        "uint64",
        "float",
        "single",
        "double",
        "decimal",
        "System.Byte",
        "System.SByte",
        "System.Int16",
        "System.UInt16",
        "System.Int32",
        "System.UInt32",
        "System.Int64",
        "System.UInt64",
        "System.Single",
        "System.Double",
        "System.Decimal"
    };

    public bool CanEdit(SocketData socket)
    {
        if (socket.IsExecution || !socket.IsInput)
        {
            return false;
        }

        var hint = socket.EditorHint?.Kind;
        if (hint is not null && hint != SocketEditorKind.Number)
        {
            return false;
        }

        var typeName = socket.TypeName ?? string.Empty;
        return NumericTypeNames.Contains(typeName);
    }

    public RenderFragment Render(SocketEditorContext context)
        => builder =>
        {
            builder.OpenComponent<NumericEditor>(0);
            builder.AddAttribute(1, nameof(NumericEditor.Context), context);
            builder.CloseComponent();
        };
}
