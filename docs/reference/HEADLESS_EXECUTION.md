# Headless Execution

NodeEditor.Net supports running node graphs without any UI. The `HeadlessGraphRunner` service loads graphs from JSON or `GraphData` objects and executes them in any .NET host—console apps, APIs, background services, or unit tests.

## Why Headless Execution?

| Use Case | Description |
|----------|-------------|
| **CI/CD Pipelines** | Run data processing graphs as part of build automation |
| **API Endpoints** | Execute graphs in response to HTTP requests |
| **Background Services** | Schedule graph execution on a timer or queue |
| **Unit Testing** | Validate graph behavior programmatically |
| **MCP Server** | The MCP `execution.run_json` ability uses headless execution |
| **Batch Processing** | Process multiple datasets through the same graph |

## Architecture

```
HeadlessGraphRunner (scoped)
├── GraphSerializer — loads JSON into GraphData
├── NodeExecutionService — executes the plan
├── NodeContextRegistry — resolves node implementations
├── SocketTypeResolver — maps type names to CLR types
└── ExecutionPlanner — builds topological execution order
```

The `HeadlessGraphRunner` bypasses all Blazor components and ViewModels. It works directly with the model layer (`NodeData`, `ConnectionData`, `GraphData`) and the execution layer.

## Usage

### Basic Execution from JSON

```csharp
using NodeEditor.Net.Services.Execution;

// Get the runner from DI
var runner = serviceProvider.GetRequiredService<HeadlessGraphRunner>();

// Load and execute a graph from a JSON file
var json = File.ReadAllText("my-graph.json");
var result = await runner.ExecuteFromJsonAsync(json, cancellationToken);
```

### Execution with Options

```csharp
var options = new NodeExecutionOptions
{
    Mode = ExecutionMode.Parallel,
    MaxDegreeOfParallelism = 8
};

var result = await runner.ExecuteFromJsonAsync(json, options, cancellationToken);
```

### Execution from GraphData

```csharp
var serializer = serviceProvider.GetRequiredService<GraphSerializer>();
var graphData = serializer.Deserialize(json);

var result = await runner.ExecuteAsync(graphData, options, cancellationToken);
```

### Using Custom Node Contexts

If your graph uses nodes defined in custom `INodeContext` classes, register them before execution:

```csharp
// Register custom node contexts
var registry = serviceProvider.GetRequiredService<NodeRegistryService>();
registry.RegisterFromAssembly(typeof(MyCustomNodes).Assembly);

// Execute
var result = await runner.ExecuteFromJsonAsync(json, cancellationToken);
```

## Console App Example

```csharp
using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Blazor.Services;
using NodeEditor.Net.Services.Execution;

var services = new ServiceCollection();
services.AddNodeEditor();
var provider = services.BuildServiceProvider();

using var scope = provider.CreateScope();
var runner = scope.ServiceProvider.GetRequiredService<HeadlessGraphRunner>();

var json = File.ReadAllText(args[0]);
await runner.ExecuteFromJsonAsync(json, CancellationToken.None);

Console.WriteLine("Graph executed successfully.");
```

## Integration with MCP

The MCP server's `execution.run_json` ability uses `HeadlessGraphRunner` internally. When an AI assistant sends a graph as JSON via MCP, it's executed headlessly without affecting the canvas state:

```
MCP Client → execute_ability("execution.run_json", { json: "..." })
    → ExecutionAbilityProvider
    → HeadlessGraphRunner.ExecuteFromJsonAsync()
    → Returns results to MCP client
```

## Variables in Headless Execution

Graph variables are supported in headless execution. The `VariableNodeExecutor` seeds variables from their default values before execution begins, and Get/Set nodes read and write the shared execution context.

## Service Registration

`HeadlessGraphRunner` is registered as a **scoped** service by `AddNodeEditor()`. In non-Blazor hosts, create a scope for each execution:

```csharp
using var scope = provider.CreateScope();
var runner = scope.ServiceProvider.GetRequiredService<HeadlessGraphRunner>();
await runner.ExecuteFromJsonAsync(json, token);
```

## Namespaces

| Type | Namespace |
|------|-----------|
| `HeadlessGraphRunner` | `NodeEditor.Net.Services.Execution` |
| `NodeExecutionOptions` | `NodeEditor.Net.Services.Execution` |
| `ExecutionMode` | `NodeEditor.Net.Services.Execution` |
| `NodeExecutionContext` | `NodeEditor.Net.Services.Execution` |
