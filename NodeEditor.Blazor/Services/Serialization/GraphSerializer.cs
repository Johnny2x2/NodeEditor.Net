using System.Text.Json;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services;
using NodeEditor.Blazor.Services.Registry;

namespace NodeEditor.Blazor.Services.Serialization;

public sealed class GraphSerializer : IGraphSerializer
{
    public const int CurrentVersion = 1;

    private readonly INodeRegistryService _registry;
    private readonly ConnectionValidator _connectionValidator;
    private readonly GraphSchemaMigrator _migrator;

    public GraphSerializer(
        INodeRegistryService registry,
        ConnectionValidator connectionValidator,
        GraphSchemaMigrator migrator)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _connectionValidator = connectionValidator ?? throw new ArgumentNullException(nameof(connectionValidator));
        _migrator = migrator ?? throw new ArgumentNullException(nameof(migrator));
    }

    public GraphData ExportToGraphData(INodeEditorState state)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        return state.ExportToGraphData();
    }

    public void Import(INodeEditorState state, GraphData graphData)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (graphData is null)
        {
            throw new ArgumentNullException(nameof(graphData));
        }

        state.LoadFromGraphData(graphData);
    }

    public GraphDto Export(INodeEditorState state)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var graphData = state.ExportToGraphData();
        return ToDto(
            graphData,
            state.Viewport,
            state.Zoom,
            state.SelectedNodeIds.ToList(),
            CurrentVersion);
    }

    public GraphImportResult Import(INodeEditorState state, GraphDto dto)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (dto is null)
        {
            throw new ArgumentNullException(nameof(dto));
        }

        if (dto.Version > CurrentVersion)
        {
            throw new NotSupportedException($"Unsupported schema version {dto.Version}.");
        }

        if (dto.Version < CurrentVersion)
        {
            dto = _migrator.MigrateToCurrent(dto);
        }

        var warnings = new List<string>();
        var graphData = ToGraphData(dto, warnings);

        state.LoadFromGraphData(graphData);

        var viewport = dto.Viewport ?? new ViewportDto(
            state.Viewport.X,
            state.Viewport.Y,
            state.Viewport.Width,
            state.Viewport.Height,
            state.Zoom);

        state.Viewport = new Rect2D(
            viewport.X,
            viewport.Y,
            viewport.Width,
            viewport.Height);
        state.Zoom = viewport.Zoom;

        var selected = dto.SelectedNodeIds ?? new List<string>();
        if (selected.Count > 0)
        {
            var validSelected = selected
                .Where(id => graphData.Nodes.Any(node => node.Data.Id == id))
                .ToList();

            if (validSelected.Count != selected.Count)
            {
                warnings.Add("Some selected nodes were missing during import.");
            }

            if (validSelected.Count > 0)
            {
                state.SelectNodes(validSelected, clearExisting: true);
            }
        }

        return warnings.Count == 0 ? GraphImportResult.Empty : new GraphImportResult(warnings);
    }

    public string SerializeGraphData(GraphData graphData)
    {
        if (graphData is null)
        {
            throw new ArgumentNullException(nameof(graphData));
        }

        var dto = ToDto(
            graphData,
            new Rect2D(0, 0, 0, 0),
            1.0,
            new List<string>(),
            graphData.SchemaVersion);

        return Serialize(dto);
    }

    public GraphData DeserializeToGraphData(string json)
    {
        var dto = Deserialize(json);

        if (dto.Version > CurrentVersion)
        {
            throw new NotSupportedException($"Unsupported schema version {dto.Version}.");
        }

        if (dto.Version < CurrentVersion)
        {
            dto = _migrator.MigrateToCurrent(dto);
        }

        return ToGraphData(dto, new List<string>());
    }

    public string Serialize(GraphDto dto)
    {
        if (dto is null)
        {
            throw new ArgumentNullException(nameof(dto));
        }

        return JsonSerializer.Serialize(dto, GraphSerializerContext.Default.GraphDto);
    }

    public GraphDto Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON payload is required.", nameof(json));
        }

        var dto = JsonSerializer.Deserialize(json, GraphSerializerContext.Default.GraphDto);
        if (dto is null)
        {
            throw new JsonException("Unable to deserialize graph JSON.");
        }

        return dto;
    }

    private GraphNodeData CreateGraphNodeData(NodeDto dto, List<string> warnings)
    {
        if (!string.IsNullOrWhiteSpace(dto.TypeId))
        {
            var exists = _registry.Definitions.Any(definition =>
                definition.Id.Equals(dto.TypeId, StringComparison.Ordinal));
            if (!exists)
            {
                warnings.Add($"Node type '{dto.TypeId}' not found. Using persisted socket snapshot for node '{dto.Id}'.");
            }
        }

        var inputs = dto.Inputs ?? new List<SocketData>();
        var outputs = dto.Outputs ?? new List<SocketData>();

        var nodeData = new NodeData(
            dto.Id,
            dto.Name,
            dto.Callable,
            dto.ExecInit,
            inputs,
            outputs,
            dto.TypeId);

        return new GraphNodeData(
            nodeData,
            new Point2D(dto.X, dto.Y),
            new Size2D(dto.Width, dto.Height));
    }

    private bool TryCreateConnection(
        IReadOnlyDictionary<string, GraphNodeData> nodes,
        ConnectionDto dto,
        List<string> warnings,
        out ConnectionData connection)
    {
        connection = default!;

        if (!nodes.TryGetValue(dto.OutputNodeId, out var outputNode))
        {
            warnings.Add($"Connection skipped: output node '{dto.OutputNodeId}' not found.");
            return false;
        }

        if (!nodes.TryGetValue(dto.InputNodeId, out var inputNode))
        {
            warnings.Add($"Connection skipped: input node '{dto.InputNodeId}' not found.");
            return false;
        }

        var outputSocket = outputNode.Data.Outputs
            .FirstOrDefault(socket => socket.Name.Equals(dto.OutputSocketName, StringComparison.Ordinal));
        if (outputSocket is null)
        {
            warnings.Add($"Connection skipped: output socket '{dto.OutputSocketName}' not found on node '{dto.OutputNodeId}'.");
            return false;
        }

        var inputSocket = inputNode.Data.Inputs
            .FirstOrDefault(socket => socket.Name.Equals(dto.InputSocketName, StringComparison.Ordinal));
        if (inputSocket is null)
        {
            warnings.Add($"Connection skipped: input socket '{dto.InputSocketName}' not found on node '{dto.InputNodeId}'.");
            return false;
        }

        if (!_connectionValidator.CanConnect(outputSocket, inputSocket))
        {
            warnings.Add($"Connection skipped: sockets '{dto.OutputSocketName}' -> '{dto.InputSocketName}' are incompatible.");
            return false;
        }

        connection = new ConnectionData(
            dto.OutputNodeId,
            dto.InputNodeId,
            dto.OutputSocketName,
            dto.InputSocketName,
            dto.IsExecution);
        return true;
    }

    private static NodeDto ToDto(GraphNodeData node)
    {
        var inputs = node.Data.Inputs.ToList();
        var outputs = node.Data.Outputs.ToList();

        return new NodeDto(
            node.Data.Id,
            node.Data.DefinitionId,
            node.Data.Name,
            node.Data.Callable,
            node.Data.ExecInit,
            node.Position.X,
            node.Position.Y,
            node.Size.Width,
            node.Size.Height,
            inputs,
            outputs);
    }

    private static ConnectionDto ToDto(ConnectionData connection)
    {
        return new ConnectionDto(
            connection.OutputNodeId,
            connection.OutputSocketName,
            connection.InputNodeId,
            connection.InputSocketName,
            connection.IsExecution);
    }

    private static GraphVariableDto ToDto(GraphVariable variable)
    {
        return new GraphVariableDto(
            variable.Id,
            variable.Name,
            variable.TypeName,
            variable.DefaultValue);
    }

    private static GraphVariable ToVariable(GraphVariableDto variable)
    {
        return new GraphVariable(
            variable.Id,
            variable.Name,
            variable.TypeName,
            variable.DefaultValue);
    }

    private GraphData ToGraphData(GraphDto dto, List<string> warnings)
    {
        var nodes = dto.Nodes ?? new List<NodeDto>();
        var connections = dto.Connections ?? new List<ConnectionDto>();
        var variables = dto.Variables ?? new List<GraphVariableDto>();

        var graphNodes = new List<GraphNodeData>();
        var nodeMap = new Dictionary<string, GraphNodeData>(StringComparer.Ordinal);
        foreach (var nodeDto in nodes)
        {
            var node = CreateGraphNodeData(nodeDto, warnings);
            nodeMap[node.Data.Id] = node;
            graphNodes.Add(node);
        }

        var graphConnections = new List<ConnectionData>();
        foreach (var connectionDto in connections)
        {
            if (!TryCreateConnection(nodeMap, connectionDto, warnings, out var connection))
            {
                continue;
            }

            graphConnections.Add(connection);
        }

        var graphVariables = variables.Select(ToVariable).ToList();

        return new GraphData(
            graphNodes,
            graphConnections,
            graphVariables,
            CurrentVersion);
    }

    private static GraphDto ToDto(
        GraphData graphData,
        Rect2D viewport,
        double zoom,
        List<string> selectedNodeIds,
        int version)
    {
        return new GraphDto(
            Version: version,
            Nodes: graphData.Nodes.Select(ToDto).ToList(),
            Connections: graphData.Connections.Select(ToDto).ToList(),
            Viewport: new ViewportDto(
                viewport.X,
                viewport.Y,
                viewport.Width,
                viewport.Height,
                zoom),
            SelectedNodeIds: selectedNodeIds,
            Variables: graphData.Variables.Select(ToDto).ToList());
    }
}
