using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Services.Editors;

public sealed class NodeEditorCustomEditorRegistry
{
    private readonly IReadOnlyList<INodeCustomEditor> _editors;

    public NodeEditorCustomEditorRegistry(IEnumerable<INodeCustomEditor> editors)
    {
        _editors = editors.ToList();
    }

    public INodeCustomEditor? GetEditor(SocketData socket)
    {
        return _editors.FirstOrDefault(editor => editor.CanEdit(socket));
    }
}
