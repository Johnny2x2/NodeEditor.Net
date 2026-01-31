using System.Collections.ObjectModel;
using System.Linq;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.ViewModels;

namespace NodeEditor.Blazor.Services;

public sealed class NodeEditorState
{
    public ObservableCollection<NodeViewModel> Nodes { get; } = new();
    public ObservableCollection<ConnectionData> Connections { get; } = new();

    public HashSet<string> SelectedNodeIds { get; } = new();

    public double Zoom { get; set; } = 1.0;

    public Rect2D Viewport { get; set; } = new(0, 0, 0, 0);

    public void SelectNode(string nodeId, bool clearExisting = true)
    {
        if (clearExisting)
        {
            ClearSelection();
        }

        var node = Nodes.FirstOrDefault(n => n.Data.Id == nodeId);
        if (node is null)
        {
            return;
        }

        SelectedNodeIds.Add(nodeId);
        node.IsSelected = true;
    }

    public void ToggleSelectNode(string nodeId)
    {
        var node = Nodes.FirstOrDefault(n => n.Data.Id == nodeId);
        if (node is null)
        {
            return;
        }

        if (SelectedNodeIds.Contains(nodeId))
        {
            SelectedNodeIds.Remove(nodeId);
            node.IsSelected = false;
        }
        else
        {
            SelectedNodeIds.Add(nodeId);
            node.IsSelected = true;
        }
    }

    public void ClearSelection()
    {
        SelectedNodeIds.Clear();
        foreach (var node in Nodes)
        {
            node.IsSelected = false;
        }
    }
}
