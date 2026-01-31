using System.Reflection;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services;
using NodeEditor.Blazor.Services.Registry;
using NodeEditor.Blazor.Services.Serialization;
using NodeEditor.Blazor.ViewModels;

namespace NodeEditor.Blazor.Tests;

public sealed class GraphSerializerTests
{
    [Fact]
    public void ExportImport_RoundTripsGraphState()
    {
        var state = new NodeEditorState();

        var node1Inputs = new List<SocketData>
        {
            new("Input", typeof(int).FullName!, true, false, SocketValue.FromObject(4))
        };
        var node1Outputs = new List<SocketData>
        {
            new("Output", typeof(int).FullName!, false, false, SocketValue.FromObject(9))
        };

        var node1 = new NodeViewModel(new NodeData(
            "node-1",
            "Add",
            false,
            false,
            node1Inputs,
            node1Outputs,
            "Test.Context.Add(System.Int32)"))
        {
            Position = new Point2D(10, 20),
            Size = new Size2D(120, 80)
        };

        var node2Inputs = new List<SocketData>
        {
            new("Input", typeof(int).FullName!, true, false, SocketValue.FromObject(1))
        };
        var node2Outputs = new List<SocketData>
        {
            new("Output", typeof(int).FullName!, false, false, SocketValue.FromObject(2))
        };

        var node2 = new NodeViewModel(new NodeData(
            "node-2",
            "Multiply",
            false,
            false,
            node2Inputs,
            node2Outputs,
            "Test.Context.Multiply(System.Int32)"))
        {
            Position = new Point2D(50, 100),
            Size = new Size2D(140, 70)
        };

        state.AddNode(node1);
        state.AddNode(node2);
        state.AddConnection(new ConnectionData("node-1", "node-2", "Output", "Input", false));

        state.Viewport = new Rect2D(0, 0, 640, 480);
        state.Zoom = 1.25;
        state.SelectNode("node-1");

        var serializer = CreateSerializer();
        var dto = serializer.Export(state);
        var json = serializer.Serialize(dto);
        var rehydratedDto = serializer.Deserialize(json);

        var importedState = new NodeEditorState();
        var result = serializer.Import(importedState, rehydratedDto);

        Assert.Empty(result.Warnings);
        Assert.Equal(2, importedState.Nodes.Count);
        Assert.Single(importedState.Connections);
        Assert.Equal(new Rect2D(0, 0, 640, 480), importedState.Viewport);
        Assert.Equal(1.25, importedState.Zoom);
        Assert.Single(importedState.SelectedNodeIds);

        var importedNode1 = importedState.Nodes.Single(n => n.Data.Id == "node-1");
        Assert.Equal(new Point2D(10, 20), importedNode1.Position);
        Assert.Equal(new Size2D(120, 80), importedNode1.Size);
        Assert.Equal(4, importedNode1.Inputs[0].Data.Value?.ToObject<int>());
        Assert.Equal(9, importedNode1.Outputs[0].Data.Value?.ToObject<int>());
    }

    [Fact]
    public void Import_SkipsInvalidConnections_WithWarnings()
    {
        var serializer = CreateSerializer();

        var nodeDto = new NodeDto(
            "node-1",
            "Test.Context.Add(System.Int32)",
            "Add",
            false,
            false,
            0,
            0,
            100,
            60,
            new List<SocketData> { new("Input", typeof(int).FullName!, true, false) },
            new List<SocketData> { new("Output", typeof(int).FullName!, false, false) });

        var dto = new GraphDto(
            GraphSerializer.CurrentVersion,
            new List<NodeDto> { nodeDto },
            new List<ConnectionDto>
            {
                new("missing-node", "Output", "node-1", "Input", false)
            },
            new ViewportDto(0, 0, 0, 0, 1),
            new List<string>());

        var state = new NodeEditorState();
        var result = serializer.Import(state, dto);

        Assert.NotEmpty(result.Warnings);
        Assert.Empty(state.Connections);
    }

    [Fact]
    public void Import_RejectsNewerSchemaVersions()
    {
        var serializer = CreateSerializer();
        var dto = new GraphDto(
            GraphSerializer.CurrentVersion + 1,
            new List<NodeDto>(),
            new List<ConnectionDto>(),
            new ViewportDto(0, 0, 0, 0, 1),
            new List<string>());

        Assert.Throws<NotSupportedException>(() => serializer.Import(new NodeEditorState(), dto));
    }

    [Fact]
    public void Import_MigratesOlderSchemaVersions()
    {
        var serializer = CreateSerializer();
        var dto = new GraphDto(
            0,
            new List<NodeDto>(),
            new List<ConnectionDto>(),
            new ViewportDto(0, 0, 0, 0, 1),
            new List<string>());

        var result = serializer.Import(new NodeEditorState(), dto);

        Assert.Empty(result.Warnings);
    }

    private static GraphSerializer CreateSerializer()
    {
        var registry = new NodeRegistryService(new NodeDiscoveryService());
        registry.EnsureInitialized(Array.Empty<Assembly>());
        registry.RegisterDefinitions(new[]
        {
            new NodeDefinition(
                "Test.Context.Add(System.Int32)",
                "Add",
                "Test",
                string.Empty,
                Array.Empty<SocketData>(),
                Array.Empty<SocketData>(),
                () => new NodeData("test-add", "Add", false, false, Array.Empty<SocketData>(), Array.Empty<SocketData>(), "Test.Context.Add(System.Int32)")),
            new NodeDefinition(
                "Test.Context.Multiply(System.Int32)",
                "Multiply",
                "Test",
                string.Empty,
                Array.Empty<SocketData>(),
                Array.Empty<SocketData>(),
                () => new NodeData("test-multiply", "Multiply", false, false, Array.Empty<SocketData>(), Array.Empty<SocketData>(), "Test.Context.Multiply(System.Int32)"))
        });

        var resolver = new SocketTypeResolver();
        var validator = new ConnectionValidator(resolver);
        var migrator = new GraphSchemaMigrator();

        return new GraphSerializer(registry, validator, migrator);
    }
}
