# Basic Node Editor Sample

A minimal working sample demonstrating how to use the NodeEditor.Blazor library in a .NET MAUI Blazor application.

## Overview

This sample shows:
- Basic NodeEditor.Blazor setup in a MAUI app
- Registration of custom node contexts (Math, Logic, String operations)
- Creating and connecting nodes programmatically
- Graph manipulation (save/load/clear)
- Toolbar integration with editor state

## Features

### Node Contexts

The sample includes three node contexts:

1. **MathNodeContext**: Mathematical operations
   - Add, Subtract, Multiply, Divide
   - Power, Square Root, Absolute Value
   - Min, Max, Clamp

2. **LogicNodeContext**: Logical operations
   - AND, OR, NOT, XOR
   - Greater Than, Less Than, Equals
   - If-Then-Else

3. **StringNodeContext**: String manipulation
   - Concatenate, ToUpper, ToLower
   - Length, Contains, Replace
   - Substring, Number↔String conversion

### UI Features

- **Toolbar**: Quick actions and graph statistics
- **Sample Graph**: Pre-built example demonstrating connections
- **Save/Load**: Graph serialization (console output)
- **Clear**: Remove all nodes and connections

## Building and Running

### Prerequisites

- .NET 8.0 SDK or later
- Visual Studio 2022 (17.8+) or JetBrains Rider
- Platform-specific requirements:
  - **Windows**: Windows 10 SDK (19041 or later)
  - **Android**: Android SDK (API 24+)
  - **iOS/macOS**: Xcode 14.0+

### Build Instructions

1. **Clone the repository** (if not already done)

2. **Navigate to sample directory**:
   ```bash
   cd samples/BasicNodeEditor
   ```

3. **Restore dependencies**:
   ```bash
   dotnet restore
   ```

4. **Build the project**:
   ```bash
   dotnet build
   ```

### Run Instructions

#### Windows

```bash
dotnet build -t:Run -f net8.0-windows10.0.19041.0
```

Or use Visual Studio:
1. Open `BasicNodeEditor.csproj`
2. Set target framework to `net8.0-windows10.0.19041.0`
3. Press F5 to run

#### Android

```bash
dotnet build -t:Run -f net8.0-android
```

#### iOS

```bash
dotnet build -t:Run -f net8.0-ios
```

#### macOS

```bash
dotnet build -t:Run -f net8.0-maccatalyst
```

## Project Structure

```
BasicNodeEditor/
├── Components/
│   ├── Editor.razor              # Main editor component
│   ├── Editor.razor.css          # Editor styles
│   └── _Imports.razor            # Component imports
├── Contexts/
│   ├── MathNodeContext.cs        # Math operations nodes
│   ├── LogicNodeContext.cs       # Logic operations nodes
│   └── StringNodeContext.cs      # String operations nodes
├── Resources/                    # MAUI resources
│   ├── Styles/
│   ├── AppIcon/
│   ├── Splash/
│   ├── Images/
│   └── Fonts/
├── wwwroot/
│   ├── index.html                # Blazor host page
│   └── css/
│       └── app.css               # Global styles
├── App.xaml                      # MAUI application definition
├── App.xaml.cs
├── MainPage.xaml                 # Main MAUI page
├── MainPage.xaml.cs
├── MauiProgram.cs                # App configuration & DI
└── BasicNodeEditor.csproj        # Project file
```

## Usage

### 1. Load Sample Graph

Click the "Load Sample Graph" button in the toolbar to create a pre-built example:
- Two Add nodes (5+3=8, 10+2=12)
- One Multiply node (8×12=96)
- Connections between them showing data flow

### 2. Add Custom Nodes

Right-click on the canvas to open the context menu and select nodes from categories:
- Math
- Logic
- String

### 3. Connect Nodes

Drag from an output socket (right side) to an input socket (left side) of another node. The connection will only succeed if socket types are compatible.

### 4. Edit Values

Click on input sockets to edit their values directly in the node component.

### 5. Pan and Zoom

- **Pan**: Middle mouse button + drag (or touch drag on mobile)
- **Zoom**: Mouse wheel (or pinch on mobile)
- **Reset**: Click Reset Viewport in context menu

### 6. Select and Move Nodes

- **Select**: Left click on a node
- **Multi-select**: Ctrl/Cmd + click
- **Move**: Drag selected nodes
- **Delete**: Press Delete key

## Code Walkthrough

### Service Registration (MauiProgram.cs)

```csharp
builder.Services.AddNodeEditor(config =>
{
    // Register node contexts
    config.RegisterNodeContext<MathNodeContext>();
    config.RegisterNodeContext<LogicNodeContext>();
    config.RegisterNodeContext<StringNodeContext>();

    // Enable performance optimizations
    config.EnableViewportCulling = true;

    // Configure socket types
    config.ConfigureSocketTypeResolver(resolver =>
    {
        resolver.RegisterType<int>("Number");
        resolver.RegisterType<double>("Number");
        resolver.RegisterType<string>("Text");
        resolver.RegisterType<bool>("Boolean");
    });
});
```

### Creating Nodes Programmatically (Editor.razor)

```csharp
// Add a node at specific position
var node = await State.AddNodeAsync(
    contextName: "Math",
    nodeTypeName: "Add",
    position: new Point2D(100, 100)
);

// Set input values
await State.SetSocketValueAsync(node.Id, "A", 5);
await State.SetSocketValueAsync(node.Id, "B", 3);
```

### Creating Connections

```csharp
await State.CreateConnectionAsync(
    sourceNodeId: addNode.Id,
    sourceSocketName: "Result",
    targetNodeId: multiplyNode.Id,
    targetSocketName: "A"
);
```

### Custom Node Context Example

```csharp
public class MathNodeContext : INodeContext
{
    [Node("Add", Category = "Math", Description = "Adds two numbers")]
    public void Add(
        [Socket("Number", DefaultValue = 0)] int a,
        [Socket("Number", DefaultValue = 0)] int b,
        [Socket("Number")] out int result)
    {
        result = a + b;
    }
}
```

## Customization

### Adding New Node Types

1. Create a new class implementing `INodeContext`
2. Add methods decorated with `[Node]` attribute
3. Define parameters with `[Socket]` attribute
4. Register in `MauiProgram.cs`

Example:
```csharp
public class MyCustomContext : INodeContext
{
    [Node("My Operation", Category = "Custom")]
    public void MyOperation(
        [Socket("Number")] int input,
        [Socket("Number")] out int output)
    {
        output = input * 2;
    }
}

// Register
config.RegisterNodeContext<MyCustomContext>();
```

### Styling

Edit `Editor.razor.css` to customize the toolbar appearance, or `wwwroot/css/app.css` for global styles.

### Adding Custom Socket Editors

See the [Custom Nodes Tutorial](../../NodeEditor.Blazor/docs/CUSTOM-NODES.md#part-4-custom-socket-editors) for details on creating custom input editors.

## Troubleshooting

### Nodes Don't Appear

- Verify context is registered in `MauiProgram.cs`
- Check that methods have `[Node]` attribute
- Ensure methods are public
- Look for errors in console

### Connections Fail

- Check socket type compatibility
- Verify SocketTypeResolver configuration
- Ensure source is output socket, target is input socket

### Performance Issues

- ViewportCulling is enabled by default in this sample
- For very large graphs (1000+ nodes), consider limiting visible nodes
- Check browser/device performance capabilities

### Build Errors

- Ensure .NET 8.0 SDK is installed
- Verify all NuGet packages are restored
- Check that NodeEditor.Blazor project reference is correct

## Next Steps

- Read the [API Reference](../../NodeEditor.Blazor/docs/API.md)
- Follow the [Custom Nodes Tutorial](../../NodeEditor.Blazor/docs/CUSTOM-NODES.md)
- Check out [Troubleshooting Guide](../../NodeEditor.Blazor/docs/TROUBLESHOOTING.md)
- Explore the [Migration Guide](../../NodeEditor.Blazor/docs/MIGRATION.md) if coming from WinForms

## License

Same as NodeEditor.Blazor - see LICENSE.txt in repository root.

## Support

For issues specific to this sample:
1. Check the console output for errors
2. Review the [Troubleshooting Guide](../../NodeEditor.Blazor/docs/TROUBLESHOOTING.md)
3. Open an issue on GitHub with reproduction steps

---

*This sample demonstrates basic usage. For production applications, add proper error handling, state persistence, and user feedback.*
