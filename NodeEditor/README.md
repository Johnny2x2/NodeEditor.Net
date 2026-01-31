# Node Editor Documentation

## Overview

The Node Editor is a visual programming framework for .NET that allows users to create, connect, and execute nodes in a graph-based interface. This system enables visual scripting similar to Unreal Engine's Blueprints or Unity's Bolt.

## Architecture

### Core Components

#### 1. **Node**
The fundamental building block representing a single operation or function.

**Key Properties:**
- `Name` - Display name of the node
- `GUID` - Unique identifier for the node
- `Callable` - Whether the node can be executed (has execution flow)
- `ExecInit` - Whether this node is the entry point for execution
- `visual` - Visual representation and positioning

**Responsibilities:**
- Manages input and output sockets
- Executes the underlying method when triggered
- Maintains visual state and position

#### 2. **nSocket (Node Socket)**
Represents connection points on nodes for data or execution flow.

**Key Properties:**
- `Name` - Socket identifier
- `Type` - Data type the socket accepts/provides
- `Input` - Whether this is an input (true) or output (false) socket
- `Value` - Current value stored in the socket
- `IsMainExecution` - Whether this socket carries execution flow

**Types of Sockets:**
- **Execution Sockets** - Control flow (gold colored connections)
- **Data Sockets** - Pass data between nodes (colored by type)

#### 3. **NodeConnection**
Links two nodes together through their sockets.

**Properties:**
- `OutputNode` / `InputNode` - The connected nodes
- `OutputSocketName` / `InputSocketName` - Which sockets are connected
- `IsExecution` - Whether this connection carries execution flow

#### 4. **NodeManager**
Central controller managing all nodes, connections, and execution.

**Key Responsibilities:**
- Maintains lists of nodes and connections
- Handles graph execution logic
- Manages the execution stack
- Provides context for node operations

**Key Methods:**
- `StartExecute()` - Begins execution from an entry node
- `Context` - Sets the INodesContext that defines available nodes

#### 5. **NodeGraph**
Handles visual rendering of the node graph.

**Responsibilities:**
- Draws nodes and connections
- Renders bezier curves for connections
- Color-codes execution vs. data connections
- Manages draw bounds and viewport

## How It Works

### 1. Defining Nodes

Nodes are created by decorating methods with the `[Node]` attribute in a class that implements `INodesContext`.

```csharp
public class MyContext : INodesContext
{
    public Node CurrentProcessingNode { get; set; }
    public event Action<string, Node, FeedbackType, object, bool> FeedbackInfo;

    [Node(Menu = "Math", IsCallable = true)]
    public void Add(int a, int b, out int result)
    {
        result = a + b;
    }

    [Node(Menu = "Logic", IsCallable = true)]
    public void PrintValue(int value, out ExecutionPath next)
    {
        Console.WriteLine(value);
        next = new ExecutionPath();
        next.Signal(); // Continue execution
    }
}
```

**Node Attribute Properties:**
- `Menu` - Category/submenu for organizing nodes
- `Category` - Additional categorization
- `IsCallable` - If true, node has execution flow sockets (Enter/Exit)

### 2. Socket Generation

Sockets are automatically created based on method signatures:

**Input Sockets:**
- Regular parameters become input sockets
- If `IsCallable = true`, adds an "Enter" execution socket

**Output Sockets:**
- `out` parameters become output sockets  
- If `IsCallable = true`, adds an "Exit" execution socket

**Example:**
```csharp
[Node(IsCallable = true)]
public void MyNode(int inputA, string inputB, out int outputC, out ExecutionPath next)
```

Creates:
- Input sockets: "Enter" (execution), "inputA" (int), "inputB" (string)
- Output sockets: "outputC" (int), "next" (ExecutionPath), "Exit" (execution)

### 3. Execution Flow

#### Standard Execution (IsCallable = true)
1. Execution starts at an entry node (`ExecInit = true`)
2. Node inputs are resolved by traversing back through connections
3. Node method executes
4. Execution follows connections from execution output sockets
5. Process continues until no more execution connections

#### Data Flow
- Data flows from output sockets to input sockets
- Values are calculated on-demand during execution
- Results are cached in socket values

#### Execution Paths
The `ExecutionPath` class controls execution branching:
```csharp
[Node(IsCallable = true)]
public void Branch(bool condition, out ExecutionPath true, out ExecutionPath false)
{
    if (condition)
    {
        true = new ExecutionPath();
        true.Signal(); // This path will execute
    }
    else
    {
        false = new ExecutionPath();
        false.Signal();
    }
}
```

### 4. Visual Rendering

#### Node Appearance
- **Header** - Contains node name
- **Sockets** - Left side for inputs, right side for outputs
- **Execution sockets** - Displayed at the top of the socket list

#### Connection Rendering
- **Gold connections** - Execution flow
- **Colored connections** - Data flow (color based on data type)
- **Bezier curves** - Smooth curved connections between sockets

### 5. Context System

The `INodesContext` interface bridges your application logic with the node editor:

```csharp
public interface INodesContext
{
    Node CurrentProcessingNode { get; set; }
    event Action<string, Node, FeedbackType, object, bool> FeedbackInfo;
}
```

**CurrentProcessingNode** - Always points to the currently executing node

**FeedbackInfo Event** - Allows nodes to send feedback during execution:
- Messages (for debugging)
- Node reference
- Feedback type (Debug, Warning, Error)
- Optional tag data
- Can signal execution break

## Usage Example

### Complete Setup

```csharp
// 1. Create your context with node definitions
public class MyNodeContext : INodesContext
{
    public Node CurrentProcessingNode { get; set; }
    public event Action<string, Node, FeedbackType, object, bool> FeedbackInfo;

    [Node(Menu = "Start", IsCallable = true)]
    public void EntryPoint(out ExecutionPath next)
    {
        Console.WriteLine("Starting execution");
        next = new ExecutionPath();
        next.Signal();
    }

    [Node(Menu = "Math")]
    public void Add(int a, int b, out int result)
    {
        result = a + b;
    }

    [Node(Menu = "Output", IsCallable = true)]
    public void Print(string message, out ExecutionPath next)
    {
        Console.WriteLine(message);
        next = new ExecutionPath();
        next.Signal();
    }
}

// 2. Initialize the editor
var nodeControl = new NodeControl();
var nodeManager = new NodeManager();
nodeManager.control = nodeControl;

// 3. Set the context
var context = new MyNodeContext();
nodeManager.Context = context;

// 4. Create nodes programmatically or through UI
// Users can add nodes, connect sockets, and build the graph

// 5. Execute the graph
var cancellationToken = new CancellationToken();
nodeManager.StartExecute(cancellationToken);
```

## Key Concepts

### 1. **Lazy Evaluation**
Input values are only computed when needed during node execution by traversing back through connections.

### 2. **Execution Stack**
The system maintains a stack to handle complex execution flows and branching paths.

### 3. **Type Safety**
Connections can only be made between compatible socket types - the system uses .NET type information.

### 4. **Visual Feedback**
Nodes provide visual feedback during execution through the `FeedbackType` enum (Debug, Warning, Error).

### 5. **Dynamic Context**
The `DynamicNodeContext` allows runtime modification of node properties and values.

## Advanced Features

### Custom Node Types

You can create specialized node types by:
1. Extending the node context
2. Using `out` parameters for multiple outputs
3. Returning `ExecutionPath` for conditional branching

### Serialization

The system includes:
- `DynamicNodeContextConverter` for JSON serialization
- `SerializableList` for graph persistence
- GUID-based node identification for save/load

### Visual Customization

`NodeVisual` and `SocketVisual` classes control appearance:
- Node dimensions
- Socket positioning
- Colors and styling
- Draw information

## Best Practices

1. **Use IsCallable for Action Nodes** - Nodes that perform actions should have `IsCallable = true`
2. **Pure Functions Don't Need IsCallable** - Math operations, data transformations can be `IsCallable = false`
3. **Signal Execution Paths** - Always call `.Signal()` on ExecutionPath outputs you want to follow
4. **Organize with Menus** - Use the `Menu` property to categorize nodes logically
5. **Provide Feedback** - Raise `FeedbackInfo` events for debugging and error handling

## Common Patterns

### Entry Point Node
```csharp
[Node(Menu = "Flow/Start", IsCallable = true)]
public void Start(out ExecutionPath next)
{
    // Mark this node as executable from the start
    CurrentProcessingNode.ExecInit = true;
    next = new ExecutionPath();
    next.Signal();
}
```

### Conditional Branch
```csharp
[Node(Menu = "Flow/Branch", IsCallable = true)]
public void If(bool condition, out ExecutionPath true, out ExecutionPath false)
{
    if (condition)
    {
        true = new ExecutionPath();
        true.Signal();
    }
    else
    {
        false = new ExecutionPath();
        false.Signal();
    }
}
```

### Pure Data Node
```csharp
[Node(Menu = "Math/Multiply")]
public void Multiply(int a, int b, out int result)
{
    result = a * b;
}
```

## Troubleshooting

**Nodes Not Executing:**
- Verify entry node has `ExecInit = true`
- Check that execution paths are signaled with `.Signal()`
- Ensure connections are properly established

**Type Mismatch:**
- Socket types must match for connections
- Check parameter types in node methods

**Visual Issues:**
- Call `DiscardCache()` on nodes after moving/modifying
- Ensure NodeGraph.Draw() is called regularly

## License

MIT License - Copyright (c) 2021 Mariusz Komorowski (komorra)
