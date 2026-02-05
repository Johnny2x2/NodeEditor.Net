using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Services.Editors;

public sealed class NodeEditorCustomEditorRegistry
{
    private readonly List<INodeCustomEditor> _editors;
    private readonly object _lock = new();

    public NodeEditorCustomEditorRegistry(IEnumerable<INodeCustomEditor> editors)
    {
        _editors = editors.ToList();
    }

    public void RegisterEditor(INodeCustomEditor editor)
    {
        if (editor is null)
        {
            return;
        }

        lock (_lock)
        {
            _editors.Insert(0, editor);
        }
    }

    public void RemoveEditors(IEnumerable<INodeCustomEditor> editors)
    {
        if (editors is null)
        {
            return;
        }

        lock (_lock)
        {
            foreach (var editor in editors)
            {
                _editors.Remove(editor);
            }
        }
    }

    public INodeCustomEditor? GetEditor(SocketData socket)
    {
        lock (_lock)
        {
            return _editors.FirstOrDefault(editor => editor.CanEdit(socket));
        }
    }
}
