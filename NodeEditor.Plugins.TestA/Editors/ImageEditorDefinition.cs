using Microsoft.AspNetCore.Components;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services.Editors;
using NodeEditor.Plugins.TestA.Components.Editors;

namespace NodeEditor.Plugins.TestA.Editors;

public sealed class ImageEditorDefinition : INodeCustomEditor
{
    public bool CanEdit(SocketData socket)
    {
        // Only edit input sockets (not outputs)
        if (socket.IsExecution || !socket.IsInput)
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

        // Only match the TestA ImagePath socket
        return socket.Name.Equals("ImagePath", StringComparison.Ordinal);
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
