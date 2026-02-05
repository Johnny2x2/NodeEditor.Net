using Microsoft.AspNetCore.Components;
using NodeEditor.Blazor.Components.Editors;
using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Services.Editors;

public sealed class TextAreaEditorDefinition : INodeCustomEditor
{
    public bool CanEdit(SocketData socket)
    {
        if (socket.IsExecution || !socket.IsInput)
        {
            return false;
        }

        return socket.EditorHint?.Kind == SocketEditorKind.TextArea;
    }

    public RenderFragment Render(SocketEditorContext context)
        => builder =>
        {
            builder.OpenComponent<TextAreaEditor>(0);
            builder.AddAttribute(1, nameof(TextAreaEditor.Context), context);
            builder.CloseComponent();
        };
}
