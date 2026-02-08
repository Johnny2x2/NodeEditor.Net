using Microsoft.AspNetCore.Components;
using NodeEditor.Blazor.Components.Editors;
using NodeEditor.Net.Models;

namespace NodeEditor.Blazor.Services.Editors;

public sealed class ImageEditorDefinition : INodeCustomEditor
{
    public bool CanEdit(SocketData socket)
    {
        // Only edit input sockets (not outputs)
        if (socket.IsExecution || !socket.IsInput)
        {
            return false;
        }

        var hint = socket.EditorHint?.Kind;
        if (hint is not null && hint != SocketEditorKind.Image)
        {
            return false;
        }

        var typeName = socket.Value?.TypeName ?? socket.TypeName;
        if (string.IsNullOrEmpty(typeName))
        {
            return false;
        }

        // Only match string types
        var isString = typeName.Equals("System.String", StringComparison.Ordinal)
            || typeName.Equals("String", StringComparison.Ordinal);
        
        if (!isString)
        {
            return false;
        }

         // Only match specific socket names for image paths
         return socket.Name.Equals("ImagePath", StringComparison.Ordinal)
             || hint == SocketEditorKind.Image;
    }

    public RenderFragment Render(SocketEditorContext context)
    {
        return builder =>
        {
            builder.OpenComponent<ImageEditor>(0);
            builder.AddAttribute(1, "Context", context);
            builder.CloseComponent();
        };
    }
}
