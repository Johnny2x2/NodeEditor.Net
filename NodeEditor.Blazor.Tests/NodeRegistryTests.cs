using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Blazor.Tests;

public sealed class NodeRegistryTests
{
    [Fact]
    public void NodeDiscoveryService_FindsAnnotatedNodes()
    {
        var discovery = new NodeDiscoveryService();
        var definitions = discovery.DiscoverFromAssemblies(new[] { typeof(RegistryTestContext).Assembly });

        Assert.Contains(definitions, d => d.Name == "Start");
        Assert.Contains(definitions, d => d.Name == "Add");
        Assert.Contains(definitions, d => d.Name == "Const");
    }

    [Fact]
    public void NodeDiscoveryService_BuildsExecutionSockets()
    {
        var discovery = new NodeDiscoveryService();
        var definitions = discovery.DiscoverFromAssemblies(new[] { typeof(RegistryTestContext).Assembly });
        var start = Assert.Single(definitions.Where(d => d.Id.Contains(nameof(RegistryTestContext)) && d.Name == "Start"));
        var add = Assert.Single(definitions.Where(d => d.Id.Contains(nameof(RegistryTestContext)) && d.Name == "Add"));

        Assert.DoesNotContain(start.Inputs, s => s.Name == "Enter");
        Assert.Contains(start.Outputs, s => s.Name == "Exit" && s.IsExecution);

        Assert.Contains(add.Inputs, s => s.Name == "Enter" && s.IsExecution);
        Assert.Contains(add.Inputs, s => s.Name == "A" && !s.IsExecution);
        Assert.Contains(add.Inputs, s => s.Name == "B" && !s.IsExecution);
        Assert.Contains(add.Outputs, s => s.Name == "Sum" && !s.IsExecution);
        Assert.Contains(add.Outputs, s => s.Name == "Exit" && s.IsExecution);
        Assert.Equal(1, add.Inputs.Count(s => s.Name == "Enter"));
        Assert.Equal(1, add.Outputs.Count(s => s.Name == "Exit"));
    }

    [Fact]
    public void NodeRegistryService_DeduplicatesDefinitions()
    {
        var discovery = new NodeDiscoveryService();
        var registry = new NodeRegistryService(discovery);
        var assembly = typeof(RegistryTestContext).Assembly;

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
        registry.RegisterFromAssembly(typeof(RegistryTestContext).Assembly);

        var catalog = registry.GetCatalog("Add");

        Assert.Contains(catalog.All, d => d.Name == "Add" && d.Category == "Math" && d.Description == "Adds");
        Assert.Contains(catalog.Categories, c => c.Name == "Math");
    }

    [Fact]
    public void NodeRegistryService_RegistersPluginAssembly()
    {
        var discovery = new NodeDiscoveryService();
        var registry = new NodeRegistryService(discovery);

        registry.RegisterPluginAssembly(typeof(RegistryTestContext).Assembly);

        Assert.Contains(registry.Definitions, d => d.Name == "Start");
    }

    private sealed class RegistryTestContext : INodeContext
    {
        [Node("Start", category: "Flow", description: "Start", isCallable: true, isExecutionInitiator: true)]
        public void Start(out ExecutionPath Exit)
        {
            Exit = new ExecutionPath();
        }

        [Node("Add", category: "Math", description: "Adds", isCallable: true)]
        public void Add(ExecutionPath Enter, int A, int B, out int Sum, out ExecutionPath Exit)
        {
            Sum = A + B;
            Exit = new ExecutionPath();
        }

        [Node("Const", category: "Math", description: "Const", isCallable: false)]
        public void Const(out int Value)
        {
            Value = 1;
        }
    }
}
