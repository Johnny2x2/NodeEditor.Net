using Microsoft.AspNetCore.Components;
using NodeEditor.Blazor.Components.Editors;
using NodeEditor.Net.Models;
using NodeEditor.Net.Services;

namespace NodeEditor.Blazor.Services.Editors;

public sealed class DropdownEditorDefinition : INodeCustomEditor
{
    private readonly ISocketTypeResolver _typeResolver;

    public DropdownEditorDefinition(ISocketTypeResolver typeResolver)
    {
        _typeResolver = typeResolver;
    }

    public bool CanEdit(SocketData socket)
    {
        if (socket.IsExecution || !socket.IsInput)
        {
            return false;
        }

        var hint = socket.EditorHint?.Kind;
        if (hint is not null)
        {
            return hint == SocketEditorKind.Dropdown;
        }

        var resolvedType = _typeResolver.Resolve(socket.TypeName);
        return resolvedType?.IsEnum == true;
    }

    public RenderFragment Render(SocketEditorContext context)
        => builder =>
        {
            builder.OpenComponent<DropdownEditor>(0);
            builder.AddAttribute(1, nameof(DropdownEditor.Context), context);
            builder.CloseComponent();
        };
}
