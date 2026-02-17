using Microsoft.AspNetCore.Components;
using NodeEditor.Blazor.Components.Editors;
using NodeEditor.Net.Models;

namespace NodeEditor.Blazor.Services.Editors;

public sealed class ButtonEditorDefinition : INodeCustomEditor
{
    public bool CanEdit(SocketData socket)
    {
        if (socket.IsExecution || !socket.IsInput)
        {
            return false;
        }

        return socket.EditorHint?.Kind == SocketEditorKind.Button;
    }

    public RenderFragment Render(SocketEditorContext context)
        => builder =>
        {
            builder.OpenComponent<ButtonEditor>(0);
            builder.AddAttribute(1, nameof(ButtonEditor.Context), context);
            builder.CloseComponent();
        };
}
