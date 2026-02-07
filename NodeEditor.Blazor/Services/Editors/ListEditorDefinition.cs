using Microsoft.AspNetCore.Components;
using NodeEditor.Blazor.Components.Editors;
using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Services.Editors;

public sealed class ListEditorDefinition : INodeCustomEditor
{
    public bool CanEdit(SocketData socket)
    {
        if (socket.IsExecution || !socket.IsInput)
        {
            return false;
        }

        var typeName = socket.TypeName ?? string.Empty;
        return typeName.Equals(typeof(SerializableList).FullName, StringComparison.OrdinalIgnoreCase)
               || typeName.Equals("SerializableList", StringComparison.OrdinalIgnoreCase)
               || typeName.Equals("NodeEditor.Blazor.Models.SerializableList", StringComparison.OrdinalIgnoreCase);
    }

    public RenderFragment Render(SocketEditorContext context)
        => builder =>
        {
            builder.OpenComponent<ListEditor>(0);
            builder.AddAttribute(1, nameof(ListEditor.Context), context);
            builder.CloseComponent();
        };
}
