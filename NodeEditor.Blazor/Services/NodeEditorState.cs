using System.Collections.ObjectModel;
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
}
