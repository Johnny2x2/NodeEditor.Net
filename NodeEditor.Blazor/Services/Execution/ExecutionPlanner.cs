using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Services.Execution;

public sealed record ExecutionLayer(IReadOnlyList<NodeData> Nodes);

public sealed record ExecutionPlan(IReadOnlyList<ExecutionLayer> Layers);

public sealed class ExecutionPlanner
{
    public ExecutionPlan BuildPlan(IReadOnlyList<NodeData> nodes, IReadOnlyList<ConnectionData> connections)
    {
        var nodeMap = nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        var incomingCount = nodes.ToDictionary(n => n.Id, _ => 0, StringComparer.Ordinal);
        var edges = nodes.ToDictionary(n => n.Id, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);

        foreach (var connection in connections)
        {
            if (!nodeMap.ContainsKey(connection.OutputNodeId) || !nodeMap.ContainsKey(connection.InputNodeId))
            {
                continue;
            }

            if (edges[connection.OutputNodeId].Add(connection.InputNodeId))
            {
                incomingCount[connection.InputNodeId]++;
            }
        }

        var layers = new List<ExecutionLayer>();
        var ready = new SortedSet<string>(incomingCount.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key), StringComparer.Ordinal);
        var remaining = new HashSet<string>(nodeMap.Keys, StringComparer.Ordinal);

        while (ready.Count > 0)
        {
            var layerNodes = new List<NodeData>();
            var current = ready.ToList();
            ready.Clear();

            foreach (var nodeId in current)
            {
                if (!remaining.Remove(nodeId))
                {
                    continue;
                }

                layerNodes.Add(nodeMap[nodeId]);

                foreach (var target in edges[nodeId])
                {
                    incomingCount[target]--;
                    if (incomingCount[target] == 0)
                    {
                        ready.Add(target);
                    }
                }
            }

            if (layerNodes.Count > 0)
            {
                layers.Add(new ExecutionLayer(layerNodes));
            }
        }

        if (remaining.Count > 0)
        {
            var fallback = remaining.Select(id => nodeMap[id]).ToList();
            layers.Add(new ExecutionLayer(fallback));
        }

        return new ExecutionPlan(layers);
    }
}
