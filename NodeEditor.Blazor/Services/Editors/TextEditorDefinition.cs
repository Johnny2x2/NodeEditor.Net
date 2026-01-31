using Microsoft.AspNetCore.Components;
using NodeEditor.Blazor.Components.Editors;
using NodeEditor.Blazor.Models;
 
namespace NodeEditor.Blazor.Services.Editors;

public sealed class TextEditorDefinition : INodeCustomEditor
{
    public bool CanEdit(SocketData socket)
    {
        if (socket.IsExecution || !socket.IsInput)
        {
            return false;
        }

        var typeName = socket.TypeName ?? string.Empty;
        return typeName.Equals("string", StringComparison.OrdinalIgnoreCase)
               || typeName.Equals(typeof(string).FullName, StringComparison.OrdinalIgnoreCase)
               || typeName.Equals(typeof(string).Name, StringComparison.OrdinalIgnoreCase)
               || typeName.Equals("System.String", StringComparison.OrdinalIgnoreCase);
    }

    public RenderFragment Render(SocketEditorContext context)
        => builder =>
        {
            builder.OpenComponent<TextEditor>(0);
            builder.AddAttribute(1, nameof(TextEditor.Context), context);
            builder.CloseComponent();
        };
}
