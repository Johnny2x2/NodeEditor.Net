using NodeEditor.Net.Models;
using NodeEditor.Net.ViewModels;

namespace NodeEditor.Net.Services.Infrastructure;

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
