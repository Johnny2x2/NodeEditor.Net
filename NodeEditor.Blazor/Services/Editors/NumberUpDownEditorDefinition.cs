using Microsoft.AspNetCore.Components;
using NodeEditor.Blazor.Components.Editors;
using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Services.Editors;

public sealed class NumberUpDownEditorDefinition : INodeCustomEditor
{
    public bool CanEdit(SocketData socket)
    {
        if (socket.IsExecution || !socket.IsInput)
        {
            return false;
        }

        return socket.EditorHint?.Kind == SocketEditorKind.NumberUpDown;
    }

    public RenderFragment Render(SocketEditorContext context)
        => builder =>
        {
            builder.OpenComponent<NumberUpDownEditor>(0);
            builder.AddAttribute(1, nameof(NumberUpDownEditor.Context), context);
            builder.CloseComponent();
        };
}
