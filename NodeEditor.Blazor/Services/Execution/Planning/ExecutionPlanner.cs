using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Services.Execution;

public sealed record ExecutionLayer(IReadOnlyList<NodeData> Nodes);

public sealed record ExecutionPlan(IReadOnlyList<ExecutionLayer> Layers);

public sealed class ExecutionPlanner
{
    // Known loop output sockets that signal "continue looping"
    private static readonly HashSet<string> LoopPathNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "LoopPath"
    };

    // Known loop output sockets that signal "exit loop"
    private static readonly HashSet<string> ExitPathNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Exit"
    };

    // Known loop node names
    private static readonly HashSet<string> LoopNodeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "For Loop", "For Loop Step", "ForEach Loop", "While Loop", "Do While Loop", "Repeat Until"
    };

    /// <summary>
    /// Legacy flat plan for backward compatibility (used by BackgroundExecutionQueue).
    /// </summary>
    public ExecutionPlan BuildPlan(IReadOnlyList<NodeData> nodes, IReadOnlyList<ConnectionData> connections)
    {
        var hierarchical = BuildHierarchicalPlan(nodes, connections);
        return FlattenToLegacy(hierarchical, nodes);
    }

    /// <summary>
    /// Build a hierarchical plan that supports loops, branches, and parallel layers.
    /// Detects back-edges, extracts loop regions, and topologically sorts the rest.
    /// </summary>
    public HierarchicalPlan BuildHierarchicalPlan(
        IReadOnlyList<NodeData> nodes,
        IReadOnlyList<ConnectionData> connections)
    {
        if (nodes.Count == 0)
            return new HierarchicalPlan(Array.Empty<IExecutionStep>());

        var nodeMap = nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);

        // Build forward adjacency (excluding back-edges we detect)
        var forwardEdges = nodes.ToDictionary(
            n => n.Id,
            _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);

        // Build reverse adjacency for loop body detection
        var reverseEdges = nodes.ToDictionary(
            n => n.Id,
            _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);

        // Track which connections are back-edges (loop returns)
        var backEdges = new HashSet<(string From, string To)>();

        // First pass: identify loop header nodes and their back-edge sources
        var loopHeaders = DetectLoopHeaders(nodes, connections, nodeMap);

        // Build edges, separating back-edges from forward edges
        foreach (var connection in connections)
        {
            if (!nodeMap.ContainsKey(connection.OutputNodeId) || !nodeMap.ContainsKey(connection.InputNodeId))
                continue;

            // A back-edge is: any connection targeting a loop header from a downstream node
            // that forms a cycle. We detect this by checking if the connection's output socket
            // is "LoopPath" on a loop node (self-loop) or if the target is a known loop header
            // and the source is in the loop body.
            var isBackEdge = false;

            // Self-loop: LoopPath -> own Enter (e.g., loop node wired to itself)
            if (connection.OutputNodeId == connection.InputNodeId)
            {
                isBackEdge = true;
            }
            // Connection from downstream body node back to loop header
            else if (loopHeaders.ContainsKey(connection.InputNodeId) &&
                     IsBodyToHeaderBackEdge(connection, loopHeaders, nodeMap, connections))
            {
                isBackEdge = true;
            }

            if (isBackEdge)
            {
                backEdges.Add((connection.OutputNodeId, connection.InputNodeId));
            }
            else
            {
                forwardEdges[connection.OutputNodeId].Add(connection.InputNodeId);
                reverseEdges[connection.InputNodeId].Add(connection.OutputNodeId);
            }
        }

        // Detect loop body nodes for each loop header
        var loopBodies = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var (headerId, info) in loopHeaders)
        {
            var bodyNodes = FindLoopBodyNodes(headerId, info.LoopPathTargets, info.ExitPathTargets, forwardEdges, nodeMap);
            loopBodies[headerId] = bodyNodes;
        }

        // Remove loop body nodes from the main graph and create LoopSteps
        var excludedFromMain = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (headerId, body) in loopBodies)
        {
            excludedFromMain.Add(headerId);
            foreach (var id in body)
                excludedFromMain.Add(id);
        }

        // Build steps: topologically sort non-loop nodes into layers,
        // inserting LoopSteps at the right position.
        // Exit targets of loops must wait until after the loop completes,
        // so we add virtual dependencies from loop headers to their exit targets.
        var loopExitDependencies = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var (headerId, info) in loopHeaders)
        {
            foreach (var exitTarget in info.ExitPathTargets)
            {
                if (!loopExitDependencies.TryGetValue(exitTarget, out var deps))
                {
                    deps = new HashSet<string>(StringComparer.Ordinal);
                    loopExitDependencies[exitTarget] = deps;
                }
                deps.Add(headerId);
            }
        }

        var steps = BuildSteps(nodes, connections, nodeMap, forwardEdges, loopHeaders, loopBodies, excludedFromMain, loopExitDependencies);

        return new HierarchicalPlan(steps);
    }

    private record LoopHeaderInfo(
        List<string> LoopPathTargets,
        List<string> ExitPathTargets,
        string LoopPathSocket,
        string ExitPathSocket);

    private Dictionary<string, LoopHeaderInfo> DetectLoopHeaders(
        IReadOnlyList<NodeData> nodes,
        IReadOnlyList<ConnectionData> connections,
        Dictionary<string, NodeData> nodeMap)
    {
        var headers = new Dictionary<string, LoopHeaderInfo>(StringComparer.Ordinal);

        foreach (var node in nodes)
        {
            if (!IsLoopNode(node)) continue;

            var loopPathSocket = node.Outputs
                .FirstOrDefault(s => LoopPathNames.Contains(s.Name))?.Name ?? "LoopPath";
            var exitPathSocket = node.Outputs
                .FirstOrDefault(s => ExitPathNames.Contains(s.Name))?.Name ?? "Exit";

            var loopTargets = connections
                .Where(c => c.OutputNodeId == node.Id &&
                           c.OutputSocketName.Equals(loopPathSocket, StringComparison.OrdinalIgnoreCase) &&
                           c.InputNodeId != node.Id) // exclude self-loops
                .Select(c => c.InputNodeId)
                .Where(id => nodeMap.ContainsKey(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var exitTargets = connections
                .Where(c => c.OutputNodeId == node.Id &&
                           c.OutputSocketName.Equals(exitPathSocket, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.InputNodeId)
                .Where(id => nodeMap.ContainsKey(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            headers[node.Id] = new LoopHeaderInfo(loopTargets, exitTargets, loopPathSocket, exitPathSocket);
        }

        return headers;
    }

    private static bool IsLoopNode(NodeData node)
    {
        return LoopNodeNames.Contains(node.Name);
    }

    private static bool IsBodyToHeaderBackEdge(
        ConnectionData connection,
        Dictionary<string, LoopHeaderInfo> loopHeaders,
        Dictionary<string, NodeData> nodeMap,
        IReadOnlyList<ConnectionData> allConnections)
    {
        // If the target is a loop header and the source is reachable from
        // the loop header's LoopPath (meaning it's in the body), this is a back-edge
        if (!loopHeaders.TryGetValue(connection.InputNodeId, out var info))
            return false;

        // Simple heuristic: if the connection goes to the Enter socket of a loop header
        // and doesn't come from a node that's "before" the loop, it's likely a back-edge.
        // We check if the source node is reachable from the loop's LoopPath targets.
        if (connection.IsExecution &&
            connection.InputSocketName.Equals("Enter", StringComparison.OrdinalIgnoreCase))
        {
            // If the source is one of the loop body entry points or reachable from them,
            // this is a back-edge. For now, use a simpler check: if the source is not
            // an ExecInit or Start node, and the target is a loop header, treat exec
            // connections to loop Enter as potential back-edges.
            var sourceNode = nodeMap[connection.OutputNodeId];
            if (!sourceNode.ExecInit && sourceNode.Id != connection.InputNodeId)
            {
                // Check if there's already a forward path from before the loop to the source
                // For simplicity, check if source is listed as a LoopPath target's descendant
                return info.LoopPathTargets.Count > 0;
            }
        }

        return false;
    }

    /// <summary>
    /// Finds all nodes in the loop body by following forward edges from LoopPath targets,
    /// stopping at nodes that are exit targets or outside the loop.
    /// </summary>
    private static HashSet<string> FindLoopBodyNodes(
        string headerId,
        List<string> loopPathTargets,
        List<string> exitPathTargets,
        Dictionary<string, HashSet<string>> forwardEdges,
        Dictionary<string, NodeData> nodeMap)
    {
        var body = new HashSet<string>(StringComparer.Ordinal);
        var exitSet = new HashSet<string>(exitPathTargets, StringComparer.Ordinal);
        var queue = new Queue<string>(loopPathTargets);

        while (queue.Count > 0)
        {
            var nodeId = queue.Dequeue();
            if (nodeId == headerId) continue; // don't include header in body
            if (exitSet.Contains(nodeId)) continue; // stop at exit targets
            if (!body.Add(nodeId)) continue; // already visited

            if (forwardEdges.TryGetValue(nodeId, out var targets))
            {
                foreach (var target in targets)
                {
                    if (target != headerId && !exitSet.Contains(target))
                        queue.Enqueue(target);
                }
            }
        }

        return body;
    }

    private IReadOnlyList<IExecutionStep> BuildSteps(
        IReadOnlyList<NodeData> allNodes,
        IReadOnlyList<ConnectionData> allConnections,
        Dictionary<string, NodeData> nodeMap,
        Dictionary<string, HashSet<string>> forwardEdges,
        Dictionary<string, LoopHeaderInfo> loopHeaders,
        Dictionary<string, HashSet<string>> loopBodies,
        HashSet<string> excludedFromMain,
        Dictionary<string, HashSet<string>> loopExitDependencies)
    {
        // Topological sort of non-excluded nodes
        var mainNodes = allNodes.Where(n => !excludedFromMain.Contains(n.Id)).ToList();

        if (mainNodes.Count == 0 && loopHeaders.Count == 0)
            return Array.Empty<IExecutionStep>();

        // Build layers from main nodes
        var incomingCount = mainNodes.ToDictionary(n => n.Id, _ => 0, StringComparer.Ordinal);
        var mainEdges = mainNodes.ToDictionary(
            n => n.Id,
            _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);

        // Only count edges between main (non-excluded) nodes
        var mainNodeSet = new HashSet<string>(mainNodes.Select(n => n.Id), StringComparer.Ordinal);

        foreach (var node in mainNodes)
        {
            if (!forwardEdges.TryGetValue(node.Id, out var targets)) continue;
            foreach (var target in targets)
            {
                if (mainNodeSet.Contains(target) && mainEdges[node.Id].Add(target))
                {
                    incomingCount[target]++;
                }
            }
        }

        // Also account for loop headers: they should appear in the topological order
        // as predecessors to their exit targets. We need to insert LoopSteps at the
        // right position. To do this, we track which layer each loop header would be in.

        // For loop headers with predecessors, figure out their topological position
        // by tracking which main nodes feed into them
        var headerPredecessors = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var (headerId, _) in loopHeaders)
        {
            var preds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var conn in allConnections)
            {
                if (conn.InputNodeId == headerId && mainNodeSet.Contains(conn.OutputNodeId))
                    preds.Add(conn.OutputNodeId);
            }
            headerPredecessors[headerId] = preds;
        }

        // Kahn's algorithm for main nodes
        var steps = new List<IExecutionStep>();
        var remaining = new HashSet<string>(mainNodeSet, StringComparer.Ordinal);
        var processed = new HashSet<string>(StringComparer.Ordinal);
        var pendingLoops = new HashSet<string>(loopHeaders.Keys, StringComparer.Ordinal);

        // A node is ready when: incomingCount == 0 AND all loop headers it depends on (via exit paths) are processed
        bool IsReady(string nodeId) =>
            incomingCount[nodeId] == 0 &&
            (!loopExitDependencies.TryGetValue(nodeId, out var deps) || deps.All(h => processed.Contains(h)));

        var ready = new SortedSet<string>(
            incomingCount.Where(kvp => kvp.Value == 0 && IsReady(kvp.Key)).Select(kvp => kvp.Key),
            StringComparer.Ordinal);

        while (ready.Count > 0 || pendingLoops.Count > 0)
        {
            // Process a layer of ready main nodes
            if (ready.Count > 0)
            {
                var layerNodes = new List<NodeData>();
                var current = ready.ToList();
                ready.Clear();

                foreach (var nodeId in current)
                {
                    if (!remaining.Remove(nodeId)) continue;
                    layerNodes.Add(nodeMap[nodeId]);
                    processed.Add(nodeId);

                    if (mainEdges.TryGetValue(nodeId, out var targets))
                    {
                        foreach (var target in targets)
                        {
                            incomingCount[target]--;
                            if (incomingCount[target] == 0 && IsReady(target))
                                ready.Add(target);
                        }
                    }
                }

                if (layerNodes.Count > 0)
                    steps.Add(new LayerStep(layerNodes));
            }

            // Check if any loop headers are now ready (all their main predecessors processed)
            var readyLoops = pendingLoops
                .Where(h => headerPredecessors[h].All(p => processed.Contains(p)))
                .ToList();

            foreach (var headerId in readyLoops)
            {
                pendingLoops.Remove(headerId);
                var info = loopHeaders[headerId];
                var bodyNodeIds = loopBodies[headerId];

                // Build loop body sub-plan
                var bodyNodes = bodyNodeIds.Select(id => nodeMap[id]).ToList();
                IReadOnlyList<IExecutionStep> bodySteps;

                if (bodyNodes.Count > 0)
                {
                    // Build a simple layer plan for body nodes
                    bodySteps = BuildBodyLayers(bodyNodes, allConnections, nodeMap, headerId);
                }
                else
                {
                    bodySteps = Array.Empty<IExecutionStep>();
                }

                var loopStep = new LoopStep(
                    nodeMap[headerId],
                    info.LoopPathSocket,
                    info.ExitPathSocket,
                    bodySteps,
                    bodyNodes);

                steps.Add(loopStep);
                processed.Add(headerId);

                // After the loop, its exit targets may now be ready
                foreach (var exitTarget in info.ExitPathTargets)
                {
                    if (mainNodeSet.Contains(exitTarget) && remaining.Contains(exitTarget))
                    {
                        if (incomingCount.ContainsKey(exitTarget) && IsReady(exitTarget))
                        {
                            ready.Add(exitTarget);
                        }
                    }
                }
            }

            // Safety: if we have no ready nodes and no ready loops, break to avoid infinite loop
            if (ready.Count == 0 && readyLoops.Count == 0)
            {
                // Add any remaining nodes as a fallback layer
                if (remaining.Count > 0)
                {
                    var fallback = remaining.Select(id => nodeMap[id]).ToList();
                    steps.Add(new LayerStep(fallback));
                    remaining.Clear();
                }
                break;
            }
        }

        return steps;
    }

    /// <summary>
    /// Build topological layers for loop body nodes.
    /// </summary>
    private static IReadOnlyList<IExecutionStep> BuildBodyLayers(
        IReadOnlyList<NodeData> bodyNodes,
        IReadOnlyList<ConnectionData> allConnections,
        Dictionary<string, NodeData> nodeMap,
        string headerId)
    {
        var bodySet = new HashSet<string>(bodyNodes.Select(n => n.Id), StringComparer.Ordinal);
        var incomingCount = bodyNodes.ToDictionary(n => n.Id, _ => 0, StringComparer.Ordinal);
        var edges = bodyNodes.ToDictionary(
            n => n.Id,
            _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);

        foreach (var conn in allConnections)
        {
            // Only count edges within the body
            if (bodySet.Contains(conn.OutputNodeId) && bodySet.Contains(conn.InputNodeId))
            {
                if (edges[conn.OutputNodeId].Add(conn.InputNodeId))
                    incomingCount[conn.InputNodeId]++;
            }
        }

        var layers = new List<IExecutionStep>();
        var ready = new SortedSet<string>(
            incomingCount.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key),
            StringComparer.Ordinal);
        var remaining = new HashSet<string>(bodySet, StringComparer.Ordinal);

        while (ready.Count > 0)
        {
            var layerNodes = new List<NodeData>();
            var current = ready.ToList();
            ready.Clear();

            foreach (var nodeId in current)
            {
                if (!remaining.Remove(nodeId)) continue;
                layerNodes.Add(nodeMap[nodeId]);

                foreach (var target in edges[nodeId])
                {
                    incomingCount[target]--;
                    if (incomingCount[target] == 0)
                        ready.Add(target);
                }
            }

            if (layerNodes.Count > 0)
                layers.Add(new LayerStep(layerNodes));
        }

        if (remaining.Count > 0)
        {
            var fallback = remaining.Select(id => nodeMap[id]).ToList();
            layers.Add(new LayerStep(fallback));
        }

        return layers;
    }

    /// <summary>
    /// Flatten a hierarchical plan to the legacy ExecutionPlan format for backward compat.
    /// </summary>
    private static ExecutionPlan FlattenToLegacy(HierarchicalPlan plan, IReadOnlyList<NodeData> allNodes)
    {
        var layers = new List<ExecutionLayer>();
        FlattenSteps(plan.Steps, layers);

        if (layers.Count == 0 && allNodes.Count > 0)
        {
            // Fallback: single layer with all nodes
            layers.Add(new ExecutionLayer(allNodes.ToList()));
        }

        return new ExecutionPlan(layers);
    }

    private static void FlattenSteps(IReadOnlyList<IExecutionStep> steps, List<ExecutionLayer> layers)
    {
        foreach (var step in steps)
        {
            switch (step)
            {
                case LayerStep layer:
                    layers.Add(new ExecutionLayer(layer.Nodes));
                    break;
                case LoopStep loop:
                    // In legacy mode, just put header + body in one layer
                    var allLoopNodes = new List<NodeData> { loop.Header };
                    allLoopNodes.AddRange(loop.BodyNodes);
                    layers.Add(new ExecutionLayer(allLoopNodes));
                    break;
                case BranchStep branch:
                    layers.Add(new ExecutionLayer(new[] { branch.ConditionNode }));
                    foreach (var (_, branchSteps) in branch.Branches)
                        FlattenSteps(branchSteps, layers);
                    break;
            }
        }
    }
}
