using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Net.Services.Execution.StandardNodes;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Blazor.Tests;

public sealed class NodeRegistryTests
{
    [Fact]
    public void Discovery_FindsNodeBaseSubclasses()
    {
        var service = new NodeDiscoveryService();
        var defs = service.DiscoverFromAssemblies(new[] { typeof(StartNode).Assembly });

        Assert.Contains(defs, d => d.Name == "Start");
        Assert.Contains(defs, d => d.Name == "For Loop");
        Assert.Contains(defs, d => d.Name == "Branch");
    }

    [Fact]
    public void Discovery_BuildsCorrectExecutionSockets()
    {
        var service = new NodeDiscoveryService();
        var defs = service.DiscoverFromAssemblies(new[] { typeof(StartNode).Assembly });

        var start = Assert.Single(defs, d => d.NodeType == typeof(StartNode));
        var branch = Assert.Single(defs, d => d.NodeType == typeof(BranchNode));

        // StartNode: execution initiator — no Enter input, has Exit output
        Assert.DoesNotContain(start.Inputs, s => s.Name == "Enter");
        Assert.Contains(start.Outputs, s => s.Name == "Exit" && s.IsExecution);

        // BranchNode: has Start execution input, Cond data input, True/False execution outputs
        Assert.Contains(branch.Inputs, s => s.Name == "Start" && s.IsExecution);
        Assert.Contains(branch.Inputs, s => s.Name == "Cond" && !s.IsExecution);
        Assert.Contains(branch.Outputs, s => s.Name == "True" && s.IsExecution);
        Assert.Contains(branch.Outputs, s => s.Name == "False" && s.IsExecution);
    }

    [Fact]
    public void NodeBuilder_CreatesValidDefinition()
    {
        var def = NodeBuilder.Create("Test")
            .Category("Tests")
            .Input<int>("Value")
            .Output<string>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<int>("Value").ToString()))
            .Build();

        Assert.Equal("Test", def.Name);
        Assert.Single(def.Inputs);
        Assert.Single(def.Outputs);
        Assert.NotNull(def.InlineExecutor);
    }

    [Fact]
    public void NodeRegistryService_DeduplicatesDefinitions()
    {
        var discovery = new NodeDiscoveryService();
        var registry = new NodeRegistryService(discovery);
        var assembly = typeof(StartNode).Assembly;

        registry.RegisterFromAssembly(assembly);
        var firstCount = registry.Definitions.Count;

        registry.RegisterFromAssembly(assembly);
        var secondCount = registry.Definitions.Count;

        Assert.Equal(firstCount, secondCount);
    }

    [Fact]
    public void NodeRegistryService_BuildsCatalogWithSearch()
    {
        var discovery = new NodeDiscoveryService();
        var registry = new NodeRegistryService(discovery);
        registry.RegisterFromAssembly(typeof(StartNode).Assembly);

        var catalog = registry.GetCatalog("Branch");

        Assert.Contains(catalog.All, d => d.Name == "Branch");
        Assert.Contains(catalog.Categories, c => c.Name == "Conditions");
    }

    [Fact]
    public void NodeRegistryService_RegistersPluginAssembly()
    {
        var discovery = new NodeDiscoveryService();
        var registry = new NodeRegistryService(discovery);

        registry.RegisterPluginAssembly(typeof(StartNode).Assembly);

        Assert.Contains(registry.Definitions, d => d.Name == "Start");
    }
}
