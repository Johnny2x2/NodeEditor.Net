using System;
using System.Collections.Generic;
using System.Linq;
using NodeEditor.Net.Models;

namespace NodeEditor.Net.Services.Execution;

public sealed class ExecutionPlanner
{
    public GraphValidationResult ValidateGraph(
        IReadOnlyList<NodeData> nodes,
        IReadOnlyList<ConnectionData> connections)
    {
        var result = new GraphValidationResult();
        ValidateDataFlowAcyclicity(nodes, connections, result);
        ValidateExecutionFlowCycles(nodes, connections, result);
        ValidateConnectedInputs(nodes, connections, result);
        ValidateReachability(nodes, connections, result);
        return result;
    }

    private static void ValidateDataFlowAcyclicity(
        IReadOnlyList<NodeData> nodes,
        IReadOnlyList<ConnectionData> connections,
        GraphValidationResult result)
    {
        if (nodes.Count == 0)
            return;

        var nodeMap = nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        var edges = nodes.ToDictionary(n => n.Id, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        var indegree = nodes.ToDictionary(n => n.Id, _ => 0, StringComparer.Ordinal);

        foreach (var connection in connections.Where(c => !c.IsExecution))
        {
            if (!nodeMap.ContainsKey(connection.OutputNodeId) || !nodeMap.ContainsKey(connection.InputNodeId))
                continue;

            if (edges[connection.OutputNodeId].Add(connection.InputNodeId))
            {
                indegree[connection.InputNodeId]++;
            }
        }

        var nodesWithDataEdges = new HashSet<string>(
            edges.Where(kvp => kvp.Value.Count > 0).Select(kvp => kvp.Key)
                .Concat(indegree.Where(kvp => kvp.Value > 0).Select(kvp => kvp.Key)),
            StringComparer.Ordinal);

        if (nodesWithDataEdges.Count == 0)
            return;

        var queue = new Queue<string>(nodesWithDataEdges.Where(id => indegree[id] == 0));
        var processed = 0;

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            processed++;

            foreach (var target in edges[id])
            {
                indegree[target]--;
                if (indegree[target] == 0)
                    queue.Enqueue(target);
            }
        }

        if (processed != nodesWithDataEdges.Count)
        {
            var cycleNodes = nodesWithDataEdges.Where(id => indegree[id] > 0).ToList();
            var cycleNames = string.Join(", ", cycleNodes.Select(id =>
                nodeMap.TryGetValue(id, out var node) ? $"{node.Name} ({id})" : id));

            result.Messages.Add(new GraphValidationMessage(
                ValidationSeverity.Error,
                $"Data-flow cycle detected involving nodes: {cycleNames}"));
        }
    }

    private static void ValidateConnectedInputs(
        IReadOnlyList<NodeData> nodes,
        IReadOnlyList<ConnectionData> connections,
        GraphValidationResult result)
    {
        var connectedInputs = new HashSet<(string NodeId, string SocketName)>(
            connections.Where(c => !c.IsExecution)
                .Select(c => (c.InputNodeId, c.InputSocketName)));

        foreach (var node in nodes)
        {
            foreach (var input in node.Inputs.Where(i => !i.IsExecution))
            {
                if (input.Value is not null)
                    continue;

                if (!connectedInputs.Contains((node.Id, input.Name)))
                {
                    result.Messages.Add(new GraphValidationMessage(
                        ValidationSeverity.Warning,
                        $"Required input '{input.Name}' on node '{node.Name}' has no connection or default value.",
                        node.Id));
                }
            }
        }
    }

    /// <summary>
    /// Detects cycles in execution-flow connections using topological sort.
    /// Execution-flow cycles (e.g., LoopBody.Exit wired back to ForLoop.Enter) cause
    /// unbounded recursion and stack overflow at runtime.
    /// </summary>
    private static void ValidateExecutionFlowCycles(
        IReadOnlyList<NodeData> nodes,
        IReadOnlyList<ConnectionData> connections,
        GraphValidationResult result)
    {
        if (nodes.Count == 0)
            return;

        var nodeMap = nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        var edges = nodes.ToDictionary(n => n.Id, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        var indegree = nodes.ToDictionary(n => n.Id, _ => 0, StringComparer.Ordinal);

        foreach (var connection in connections.Where(c => c.IsExecution))
        {
            if (!nodeMap.ContainsKey(connection.OutputNodeId) || !nodeMap.ContainsKey(connection.InputNodeId))
                continue;

            if (edges[connection.OutputNodeId].Add(connection.InputNodeId))
            {
                indegree[connection.InputNodeId]++;
            }
        }

        var nodesWithExecEdges = new HashSet<string>(
            edges.Where(kvp => kvp.Value.Count > 0).Select(kvp => kvp.Key)
                .Concat(indegree.Where(kvp => kvp.Value > 0).Select(kvp => kvp.Key)),
            StringComparer.Ordinal);

        if (nodesWithExecEdges.Count == 0)
            return;

        var queue = new Queue<string>(nodesWithExecEdges.Where(id => indegree[id] == 0));
        var processed = 0;

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            processed++;

            foreach (var target in edges[id])
            {
                indegree[target]--;
                if (indegree[target] == 0)
                    queue.Enqueue(target);
            }
        }

        if (processed != nodesWithExecEdges.Count)
        {
            var cycleNodes = nodesWithExecEdges.Where(id => indegree[id] > 0).ToList();
            var cycleNames = string.Join(", ", cycleNodes.Select(id =>
                nodeMap.TryGetValue(id, out var node) ? $"{node.Name} ({id})" : id));

            result.Messages.Add(new GraphValidationMessage(
                ValidationSeverity.Warning,
                $"Execution-flow cycle detected involving nodes: {cycleNames}. " +
                $"This may cause stack overflow at runtime."));
        }
    }

    private static void ValidateReachability(
        IReadOnlyList<NodeData> nodes,
        IReadOnlyList<ConnectionData> connections,
        GraphValidationResult result)
    {
        var nodeMap = nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        var edges = nodes.ToDictionary(n => n.Id, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);

        foreach (var connection in connections.Where(c => c.IsExecution))
        {
            if (!nodeMap.ContainsKey(connection.OutputNodeId) || !nodeMap.ContainsKey(connection.InputNodeId))
                continue;

            edges[connection.OutputNodeId].Add(connection.InputNodeId);
        }

        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>(nodes.Where(n => n.ExecInit).Select(n => n.Id));

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!reachable.Add(id))
                continue;

            foreach (var target in edges[id])
            {
                if (!reachable.Contains(target))
                    queue.Enqueue(target);
            }
        }

        foreach (var node in nodes.Where(n => n.Callable && !reachable.Contains(n.Id)))
        {
            result.Messages.Add(new GraphValidationMessage(
                ValidationSeverity.Info,
                $"Callable node '{node.Name}' is unreachable from any initiator.",
                node.Id));
        }
    }
}

public sealed class GraphValidationResult
{
    public List<GraphValidationMessage> Messages { get; } = new();
    public bool HasErrors => Messages.Any(m => m.Severity == ValidationSeverity.Error);
}

public sealed record GraphValidationMessage(
    ValidationSeverity Severity,
    string Message,
    string? NodeId = null);

public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}
