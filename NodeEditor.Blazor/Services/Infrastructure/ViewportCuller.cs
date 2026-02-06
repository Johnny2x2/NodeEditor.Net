using System.Linq;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.ViewModels;

namespace NodeEditor.Blazor.Services;

/// <summary>
/// Computes visible nodes and connections based on the current viewport.
/// </summary>
public sealed class ViewportCuller : IViewportCuller
{
    private readonly ICoordinateConverter _coordinateConverter;

    public ViewportCuller(ICoordinateConverter coordinateConverter)
    {
        _coordinateConverter = coordinateConverter;
    }

    public IReadOnlyList<NodeViewModel> GetVisibleNodes(
        IReadOnlyList<NodeViewModel> nodes,
        Rect2D screenViewport,
        IEnumerable<string>? alwaysIncludeNodeIds = null)
    {
        if (nodes.Count == 0)
        {
            return Array.Empty<NodeViewModel>();
        }

        var graphViewport = _coordinateConverter.ScreenToGraph(screenViewport);
        var includeIds = alwaysIncludeNodeIds is null
            ? null
            : new HashSet<string>(alwaysIncludeNodeIds);

        var results = new List<NodeViewModel>(nodes.Count);

        foreach (var node in nodes)
        {
            if (includeIds != null && includeIds.Contains(node.Data.Id))
            {
                results.Add(node);
                continue;
            }

            var nodeRect = new Rect2D(node.Position.X, node.Position.Y, node.Size.Width, node.Size.Height);
            if (graphViewport.Intersects(nodeRect))
            {
                results.Add(node);
            }
        }

        return results;
    }

    public IReadOnlyList<ConnectionData> GetVisibleConnections(
        IReadOnlyList<ConnectionData> connections,
        IReadOnlyCollection<NodeViewModel> visibleNodes,
        IEnumerable<string>? alwaysIncludeNodeIds = null)
    {
        if (connections.Count == 0)
        {
            return Array.Empty<ConnectionData>();
        }

        var visibleNodeIds = new HashSet<string>(visibleNodes.Select(n => n.Data.Id));
        if (alwaysIncludeNodeIds is not null)
        {
            visibleNodeIds.UnionWith(alwaysIncludeNodeIds);
        }

        var results = new List<ConnectionData>(connections.Count);
        foreach (var connection in connections)
        {
            if (visibleNodeIds.Contains(connection.OutputNodeId) || visibleNodeIds.Contains(connection.InputNodeId))
            {
                results.Add(connection);
            }
        }

        return results;
    }
}