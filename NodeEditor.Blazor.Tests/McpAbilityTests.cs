using System.Text.Json;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services;
using NodeEditor.Blazor.ViewModels;
using NodeEditor.Mcp.Abilities;

namespace NodeEditor.Blazor.Tests;

public sealed class McpAbilityRegistryTests
{
    [Fact]
    public void AbilityRegistry_RegisterAndGetAll_ReturnsAbilities()
    {
        var registry = new AbilityRegistry();
        var state = new NodeEditorState();
        var nodeRegistry = CreateMinimalNodeRegistry();

        registry.Register(new NodeAbilityProvider(state, nodeRegistry));

        var abilities = registry.GetAll();
        Assert.NotEmpty(abilities);
        Assert.All(abilities, a => Assert.False(string.IsNullOrWhiteSpace(a.Id)));
    }

    [Fact]
    public void AbilityRegistry_GetById_ReturnsCorrectAbility()
    {
        var registry = new AbilityRegistry();
        var state = new NodeEditorState();
        registry.Register(new ConnectionAbilityProvider(state));

        var ability = registry.GetById("connection.add");
        Assert.NotNull(ability);
        Assert.Equal("Add Connection", ability.Name);
        Assert.Equal("Connections", ability.Category);
    }

    [Fact]
    public void AbilityRegistry_GetById_ReturnsNullForUnknown()
    {
        var registry = new AbilityRegistry();
        Assert.Null(registry.GetById("nonexistent"));
    }

    [Fact]
    public void AbilityRegistry_GetCategories_ReturnsDistinctCategories()
    {
        var registry = new AbilityRegistry();
        var state = new NodeEditorState();
        registry.Register(new NodeAbilityProvider(state, CreateMinimalNodeRegistry()));
        registry.Register(new ConnectionAbilityProvider(state));
        registry.Register(new OverlayAbilityProvider(state));

        var categories = registry.GetCategories();
        Assert.Contains("Nodes", categories);
        Assert.Contains("Connections", categories);
        Assert.Contains("Organization", categories);
    }

    [Fact]
    public void AbilityRegistry_Search_FindsMatchingAbilities()
    {
        var registry = new AbilityRegistry();
        var state = new NodeEditorState();
        registry.Register(new NodeAbilityProvider(state, CreateMinimalNodeRegistry()));
        registry.Register(new ConnectionAbilityProvider(state));

        var results = registry.Search("connection");
        Assert.NotEmpty(results);
        Assert.All(results, a =>
            Assert.True(
                a.Name.Contains("Connection", StringComparison.OrdinalIgnoreCase) ||
                a.Summary.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                a.Category.Contains("Connection", StringComparison.OrdinalIgnoreCase) ||
                a.Id.Contains("connection", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void AbilityRegistry_GetByCategory_FiltersCorrectly()
    {
        var registry = new AbilityRegistry();
        var state = new NodeEditorState();
        registry.Register(new NodeAbilityProvider(state, CreateMinimalNodeRegistry()));
        registry.Register(new ConnectionAbilityProvider(state));

        var nodeAbilities = registry.GetByCategory("Nodes");
        Assert.NotEmpty(nodeAbilities);
        Assert.All(nodeAbilities, a => Assert.Equal("Nodes", a.Category));
    }

    [Fact]
    public async Task AbilityRegistry_Execute_ReturnsNotFoundForUnknown()
    {
        var registry = new AbilityRegistry();
        var state = new NodeEditorState();
        registry.Register(new NodeAbilityProvider(state, CreateMinimalNodeRegistry()));

        var result = await registry.ExecuteAsync("nonexistent", JsonDocument.Parse("{}").RootElement);
        Assert.False(result.Success);
        Assert.Contains("not found", result.Message!);
    }

    private static Services.Registry.INodeRegistryService CreateMinimalNodeRegistry()
    {
        // Use the real NodeRegistryService but don't initialize with any assemblies
        return new Services.Registry.NodeRegistryService(
            new Services.Registry.NodeDiscoveryService());
    }
}

public sealed class McpNodeAbilityTests
{
    [Fact]
    public async Task NodeAbility_ListNodes_EmptyState()
    {
        var (provider, _) = CreateProvider();

        var result = await provider.ExecuteAsync("node.list", EmptyParams());
        Assert.True(result.Success);
        Assert.Contains("0 node(s)", result.Message!);
    }

    [Fact]
    public async Task NodeAbility_AddNode_RequiresDefinitionId()
    {
        var (provider, _) = CreateProvider();

        var result = await provider.ExecuteAsync("node.add", EmptyParams());
        Assert.False(result.Success);
        Assert.Contains("definitionId", result.Message!);
    }

    [Fact]
    public async Task NodeAbility_MoveNode_MovesCorrectly()
    {
        var (provider, state) = CreateProvider();

        var nodeData = new NodeData("n1", "TestNode", false, false,
            Array.Empty<SocketData>(), Array.Empty<SocketData>());
        var vm = new NodeViewModel(nodeData) { Position = new Point2D(0, 0) };
        state.AddNode(vm);

        var moveParams = JsonDocument.Parse("""{"nodeId":"n1","x":100,"y":200}""").RootElement;
        var result = await provider.ExecuteAsync("node.move", moveParams);

        Assert.True(result.Success);
        Assert.Equal(100, vm.Position.X);
        Assert.Equal(200, vm.Position.Y);
    }

    [Fact]
    public async Task NodeAbility_SelectNodes_SelectsCorrectly()
    {
        var (provider, state) = CreateProvider();

        var n1 = new NodeData("n1", "Node1", false, false,
            Array.Empty<SocketData>(), Array.Empty<SocketData>());
        var n2 = new NodeData("n2", "Node2", false, false,
            Array.Empty<SocketData>(), Array.Empty<SocketData>());
        state.AddNode(new NodeViewModel(n1));
        state.AddNode(new NodeViewModel(n2));

        var selectParams = JsonDocument.Parse("""{"nodeIds":["n1","n2"]}""").RootElement;
        var result = await provider.ExecuteAsync("node.select", selectParams);

        Assert.True(result.Success);
        Assert.Contains("n1", state.SelectedNodeIds);
        Assert.Contains("n2", state.SelectedNodeIds);
    }

    [Fact]
    public async Task NodeAbility_RemoveNode_RemovesCorrectly()
    {
        var (provider, state) = CreateProvider();

        var nodeData = new NodeData("n1", "TestNode", false, false,
            Array.Empty<SocketData>(), Array.Empty<SocketData>());
        state.AddNode(new NodeViewModel(nodeData));
        Assert.Single(state.Nodes);

        var removeParams = JsonDocument.Parse("""{"nodeId":"n1"}""").RootElement;
        var result = await provider.ExecuteAsync("node.remove", removeParams);

        Assert.True(result.Success);
        Assert.Empty(state.Nodes);
    }

    [Fact]
    public async Task NodeAbility_GetNode_ReturnsDetails()
    {
        var (provider, state) = CreateProvider();

        var inputs = new[] { new SocketData("Input1", "String", true, false) };
        var outputs = new[] { new SocketData("Output1", "String", false, false) };
        var nodeData = new NodeData("n1", "TestNode", false, false, inputs, outputs);
        state.AddNode(new NodeViewModel(nodeData));

        var getParams = JsonDocument.Parse("""{"nodeId":"n1"}""").RootElement;
        var result = await provider.ExecuteAsync("node.get", getParams);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task NodeAbility_ClearSelection_Works()
    {
        var (provider, state) = CreateProvider();

        var nodeData = new NodeData("n1", "TestNode", false, false,
            Array.Empty<SocketData>(), Array.Empty<SocketData>());
        state.AddNode(new NodeViewModel(nodeData));
        state.SelectNode("n1");
        Assert.NotEmpty(state.SelectedNodeIds);

        var result = await provider.ExecuteAsync("node.clear_selection", EmptyParams());
        Assert.True(result.Success);
        Assert.Empty(state.SelectedNodeIds);
    }

    private static (NodeAbilityProvider, NodeEditorState) CreateProvider()
    {
        var state = new NodeEditorState();
        var registry = new Services.Registry.NodeRegistryService(
            new Services.Registry.NodeDiscoveryService());
        return (new NodeAbilityProvider(state, registry), state);
    }

    private static JsonElement EmptyParams() => JsonDocument.Parse("{}").RootElement;
}

public sealed class McpConnectionAbilityTests
{
    [Fact]
    public async Task ConnectionAbility_ListConnections_EmptyState()
    {
        var state = new NodeEditorState();
        var provider = new ConnectionAbilityProvider(state);

        var result = await provider.ExecuteAsync("connection.list", EmptyParams());
        Assert.True(result.Success);
        Assert.Contains("0 connection(s)", result.Message!);
    }

    [Fact]
    public async Task ConnectionAbility_AddConnection_ValidatesNodes()
    {
        var state = new NodeEditorState();
        var provider = new ConnectionAbilityProvider(state);

        var p = JsonDocument.Parse("""{"outputNodeId":"x","outputSocketName":"Out","inputNodeId":"y","inputSocketName":"In"}""").RootElement;
        var result = await provider.ExecuteAsync("connection.add", p);
        Assert.False(result.Success);
        Assert.Contains("not found", result.Message!);
    }

    [Fact]
    public async Task ConnectionAbility_AddAndListConnection()
    {
        var state = new NodeEditorState();
        var provider = new ConnectionAbilityProvider(state);

        var outputs = new[] { new SocketData("Out", "String", false, false) };
        var inputs = new[] { new SocketData("In", "String", true, false) };
        state.AddNode(new NodeViewModel(new NodeData("n1", "Node1", false, false, Array.Empty<SocketData>(), outputs)));
        state.AddNode(new NodeViewModel(new NodeData("n2", "Node2", false, false, inputs, Array.Empty<SocketData>())));

        var addParams = JsonDocument.Parse("""{"outputNodeId":"n1","outputSocketName":"Out","inputNodeId":"n2","inputSocketName":"In"}""").RootElement;
        var addResult = await provider.ExecuteAsync("connection.add", addParams);
        Assert.True(addResult.Success);

        var listResult = await provider.ExecuteAsync("connection.list", EmptyParams());
        Assert.True(listResult.Success);
        Assert.Contains("1 connection(s)", listResult.Message!);
    }

    private static JsonElement EmptyParams() => JsonDocument.Parse("{}").RootElement;
}

public sealed class McpOverlayAbilityTests
{
    [Fact]
    public async Task OverlayAbility_AddAndListOverlay()
    {
        var state = new NodeEditorState();
        var provider = new OverlayAbilityProvider(state);

        var addParams = JsonDocument.Parse("""{"title":"Test Section","body":"Some notes","x":50,"y":100}""").RootElement;
        var addResult = await provider.ExecuteAsync("overlay.add", addParams);
        Assert.True(addResult.Success);
        Assert.Contains("Test Section", addResult.Message!);

        var listResult = await provider.ExecuteAsync("overlay.list", EmptyParams());
        Assert.True(listResult.Success);
        Assert.Contains("1 overlay(s)", listResult.Message!);
    }

    [Fact]
    public async Task OverlayAbility_RemoveOverlay()
    {
        var state = new NodeEditorState();
        var provider = new OverlayAbilityProvider(state);

        var data = new OverlayData("o1", "Test", "", new Point2D(0, 0), new Size2D(100, 100), "#000", 0.5);
        state.AddOverlay(new OverlayViewModel(data));
        Assert.Single(state.Overlays);

        var removeParams = JsonDocument.Parse("""{"overlayId":"o1"}""").RootElement;
        var result = await provider.ExecuteAsync("overlay.remove", removeParams);
        Assert.True(result.Success);
        Assert.Empty(state.Overlays);
    }

    private static JsonElement EmptyParams() => JsonDocument.Parse("{}").RootElement;
}

public sealed class McpGraphAbilityTests
{
    [Fact]
    public async Task GraphAbility_Summary_ShowsCounts()
    {
        var state = new NodeEditorState();
        var serializer = CreateSerializer();
        var provider = new GraphAbilityProvider(state, serializer);

        var nodeData = new NodeData("n1", "TestNode", false, false,
            Array.Empty<SocketData>(), Array.Empty<SocketData>());
        state.AddNode(new NodeViewModel(nodeData));

        var result = await provider.ExecuteAsync("graph.summary", EmptyParams());
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task GraphAbility_Clear_ClearsGraph()
    {
        var state = new NodeEditorState();
        var serializer = CreateSerializer();
        var provider = new GraphAbilityProvider(state, serializer);

        state.AddNode(new NodeViewModel(new NodeData("n1", "N1", false, false,
            Array.Empty<SocketData>(), Array.Empty<SocketData>())));
        Assert.Single(state.Nodes);

        var result = await provider.ExecuteAsync("graph.clear", EmptyParams());
        Assert.True(result.Success);
        Assert.Empty(state.Nodes);
    }

    [Fact]
    public async Task GraphAbility_VariableAdd_AddsVariable()
    {
        var state = new NodeEditorState();
        var serializer = CreateSerializer();
        var provider = new GraphAbilityProvider(state, serializer);

        var addParams = JsonDocument.Parse("""{"name":"MyVar","type":"String"}""").RootElement;
        var result = await provider.ExecuteAsync("graph.variable_add", addParams);

        Assert.True(result.Success);
        Assert.Single(state.Variables);
        Assert.Equal("MyVar", state.Variables[0].Name);
    }

    [Fact]
    public async Task GraphAbility_EventAdd_AddsEvent()
    {
        var state = new NodeEditorState();
        var serializer = CreateSerializer();
        var provider = new GraphAbilityProvider(state, serializer);

        var addParams = JsonDocument.Parse("""{"name":"MyEvent"}""").RootElement;
        var result = await provider.ExecuteAsync("graph.event_add", addParams);

        Assert.True(result.Success);
        Assert.Single(state.Events);
        Assert.Equal("MyEvent", state.Events[0].Name);
    }

    private static Services.Serialization.IGraphSerializer CreateSerializer()
    {
        var registry = new Services.Registry.NodeRegistryService(
            new Services.Registry.NodeDiscoveryService());
        var typeResolver = new SocketTypeResolver();
        var validator = new ConnectionValidator(typeResolver);
        var migrator = new Services.Serialization.GraphSchemaMigrator();
        return new Services.Serialization.GraphSerializer(registry, validator, migrator);
    }

    private static JsonElement EmptyParams() => JsonDocument.Parse("{}").RootElement;
}
