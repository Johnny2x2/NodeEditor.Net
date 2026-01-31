using Microsoft.AspNetCore.Components;
using NodeEditor.Blazor.Components.Editors;
using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Services.Editors;

public sealed class BoolEditorDefinition : INodeCustomEditor
{
    public bool CanEdit(SocketData socket)
    {
        if (socket.IsExecution || !socket.IsInput)
        {
            return false;
        }

        var typeName = socket.TypeName ?? string.Empty;
        return typeName.Equals("bool", StringComparison.OrdinalIgnoreCase)
               || typeName.Equals("boolean", StringComparison.OrdinalIgnoreCase)
               || typeName.Equals(typeof(bool).FullName, StringComparison.OrdinalIgnoreCase)
               || typeName.Equals("System.Boolean", StringComparison.OrdinalIgnoreCase);
    }

    public RenderFragment Render(SocketEditorContext context)
        => builder =>
        {
            builder.OpenComponent<BoolEditor>(0);
            builder.AddAttribute(1, nameof(BoolEditor.Context), context);
            builder.CloseComponent();
        };
}
