using System.Text.Json;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services.Registry;
using NodeEditor.Blazor.ViewModels;

namespace NodeEditor.Blazor.Services.Serialization;

public sealed class GraphSerializer
{
    public const int CurrentVersion = 1;

    private readonly NodeRegistryService _registry;
    private readonly ConnectionValidator _connectionValidator;
    private readonly GraphSchemaMigrator _migrator;

    public GraphSerializer(
        NodeRegistryService registry,
        ConnectionValidator connectionValidator,
        GraphSchemaMigrator migrator)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _connectionValidator = connectionValidator ?? throw new ArgumentNullException(nameof(connectionValidator));
        _migrator = migrator ?? throw new ArgumentNullException(nameof(migrator));
    }

    public GraphDto Export(NodeEditorState state)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        return new GraphDto(
            Version: CurrentVersion,
            Nodes: state.Nodes.Select(ToDto).ToList(),
            Connections: state.Connections.Select(ToDto).ToList(),
            Viewport: new ViewportDto(
                state.Viewport.X,
                state.Viewport.Y,
                state.Viewport.Width,
                state.Viewport.Height,
                state.Zoom),
            SelectedNodeIds: state.SelectedNodeIds.ToList());
    }

    public GraphImportResult Import(NodeEditorState state, GraphDto dto)
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
        var nodes = dto.Nodes ?? new List<NodeDto>();
        var connections = dto.Connections ?? new List<ConnectionDto>();
        var selected = dto.SelectedNodeIds ?? new List<string>();

        state.Clear();

        var nodeMap = new Dictionary<string, NodeViewModel>(StringComparer.Ordinal);
        foreach (var nodeDto in nodes)
        {
            var node = CreateNode(nodeDto, warnings);
            nodeMap[node.Data.Id] = node;
            state.AddNode(node);
        }

        foreach (var connectionDto in connections)
        {
            if (!TryCreateConnection(nodeMap, connectionDto, warnings, out var connection))
            {
                continue;
            }

            state.AddConnection(connection);
        }

        state.Viewport = new Rect2D(
            dto.Viewport.X,
            dto.Viewport.Y,
            dto.Viewport.Width,
            dto.Viewport.Height);
        state.Zoom = dto.Viewport.Zoom;

        if (selected.Count > 0)
        {
            var validSelected = selected.Where(nodeMap.ContainsKey).ToList();
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

    private NodeViewModel CreateNode(NodeDto dto, List<string> warnings)
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

        var node = new NodeViewModel(nodeData)
        {
            Position = new Point2D(dto.X, dto.Y),
            Size = new Size2D(dto.Width, dto.Height),
            IsSelected = false
        };

        return node;
    }

    private bool TryCreateConnection(
        IReadOnlyDictionary<string, NodeViewModel> nodes,
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

        var outputSocket = outputNode.Outputs
            .Select(socket => socket.Data)
            .FirstOrDefault(socket => socket.Name.Equals(dto.OutputSocketName, StringComparison.Ordinal));
        if (outputSocket is null)
        {
            warnings.Add($"Connection skipped: output socket '{dto.OutputSocketName}' not found on node '{dto.OutputNodeId}'.");
            return false;
        }

        var inputSocket = inputNode.Inputs
            .Select(socket => socket.Data)
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

    private static NodeDto ToDto(NodeViewModel node)
    {
        var inputs = node.Inputs.Select(socket => socket.Data).ToList();
        var outputs = node.Outputs.Select(socket => socket.Data).ToList();

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
}
