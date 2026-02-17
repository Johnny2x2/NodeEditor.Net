using Microsoft.AspNetCore.Components;
using NodeEditor.Net.Models;

namespace NodeEditor.Blazor.Services.Editors;

public interface INodeCustomEditor
{
    bool CanEdit(SocketData socket);

    RenderFragment Render(SocketEditorContext context);
}
