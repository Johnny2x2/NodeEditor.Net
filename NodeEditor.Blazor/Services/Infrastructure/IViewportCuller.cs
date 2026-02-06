using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.ViewModels;

namespace NodeEditor.Blazor.Services;

public interface IViewportCuller
{
    IReadOnlyList<NodeViewModel> GetVisibleNodes(IEnumerable<NodeViewModel> nodes, Rect2D visibleRect);
    IReadOnlyList<ConnectionData> GetVisibleConnections(
        IEnumerable<ConnectionData> connections,
        IEnumerable<NodeViewModel> nodes,
        Rect2D visibleRect);
}
