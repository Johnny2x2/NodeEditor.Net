using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.ViewModels;

namespace NodeEditor.Blazor.Services;

public interface IViewportCuller
{
    IReadOnlyList<NodeViewModel> GetVisibleNodes(
        IReadOnlyList<NodeViewModel> nodes,
        Rect2D screenViewport,
        IEnumerable<string>? alwaysIncludeNodeIds = null);

    IReadOnlyList<ConnectionData> GetVisibleConnections(
        IReadOnlyList<ConnectionData> connections,
        IReadOnlyCollection<NodeViewModel> visibleNodes,
        IEnumerable<string>? alwaysIncludeNodeIds = null);
}
