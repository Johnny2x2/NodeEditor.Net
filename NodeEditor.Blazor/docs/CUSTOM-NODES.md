# Custom Nodes Tutorial

Learn how to create custom nodes and custom socket editors for NodeEditor.Blazor.

## Table of Contents

- [Overview](#overview)
- [Part 1: Creating Your First Custom Node](#part-1-creating-your-first-custom-node)
- [Part 2: Working with Execution Flow](#part-2-working-with-execution-flow)
- [Part 3: Advanced Node Features](#part-3-advanced-node-features)
- [Part 4: Standard Socket Editors (Attribute-Based)](#part-4-standard-socket-editors-attribute-based)
- [Part 5: Custom Socket Editors](#part-5-custom-socket-editors)
- [Part 6: Creating a Plugin](#part-6-creating-a-plugin)
- [Best Practices](#best-practices)

---

## Overview

Custom nodes allow you to extend the node editor with domain-specific functionality. Nodes are defined as methods on classes implementing `INodeContext`, using the `[Node]` attribute for metadata.

### Node Types

1. **Data Nodes** - Process inputs and produce outputs (no execution flow)
2. **Executable Nodes** - Have execution flow control
3. **Entry Nodes** - Start execution automatically
4. **Branching Nodes** - Conditional execution paths

---

## Part 1: Creating Your First Custom Node

### Step 1: Create a Node Context Class

```csharp
using NodeEditor.Net.Services.Registry;
using NodeEditor.Net.Services.Execution;

namespace MyApp.Nodes;

public class MathContext : INodeContext
{
    public event EventHandler<FeedbackEventArgs>? FeedbackInfo;
    public event EventHandler<FeedbackEventArgs>? FeedbackWarning;
    public event EventHandler<FeedbackEventArgs>? FeedbackError;

    [Node("Add", category: "Math", description: "Add two numbers")]
    public void Add(double A, double B, out double Result)
    {
        Result = A + B;
    }

    [Node("Multiply", category: "Math", description: "Multiply two numbers")]
    public void Multiply(double A, double B, out double Result)
    {
        Result = A * B;
    }

    [Node("Power", category: "Math", description: "Raise A to the power of B")]
    public void Power(double A, double B, out double Result)
    {
        Result = Math.Pow(A, B);
    }
}
```

### Step 2: Register the Context

**Option A: Register in DI (recommended)**

```csharp
// Program.cs
builder.Services.AddNodeEditor();
builder.Services.AddScoped<MathContext>();

// Later, get from DI when executing
var mathContext = serviceProvider.GetRequiredService<MathContext>();
```

**Option B: Create manually when needed**

```csharp
var mathContext = new MathContext();
```

### Step 3: Initialize the Registry

```razor
@inject NodeRegistryService Registry

@code {
    protected override void OnInitialized()
    {
        // Register nodes from your assembly
        Registry.EnsureInitialized(new[] { typeof(MathContext).Assembly });
    }
}
```

### Step 4: Nodes Appear Automatically

Right-click on the canvas → "Math" category → Select "Add", "Multiply", or "Power"

---

## Part 2: Working with Execution Flow

### Executable Nodes

Nodes with execution flow use `ExecutionPath` parameters:

```csharp
[Node("Print", category: "Debug", description: "Print value to console", isCallable: true)]
public void Print(ExecutionPath Entry, string Message, out ExecutionPath Exit)
{
    Console.WriteLine(Message);
    Exit = new ExecutionPath();
    Exit.Signal(); // Continue execution
}
```

**Key points:**
- Input execution sockets are `ExecutionPath` parameters
- Output execution sockets are `out ExecutionPath` parameters
- Call `Signal()` to continue execution flow
- Set `isCallable: true` in the attribute

### Entry Nodes

Entry nodes start execution automatically:

```csharp
[Node("On Start", category: "Events", description: "Executes when the graph starts", isCallable: true, isExecInit: true)]
public void OnStart(out ExecutionPath Exit)
{
    FeedbackInfo?.Invoke(this, new FeedbackEventArgs("Graph started", FeedbackType.Info));
    Exit = new ExecutionPath();
    Exit.Signal();
}
```

**Key points:**
- Set `isExecInit: true` for entry nodes
- No input execution paths
- Always have at least one output execution path

### Branching Nodes

Nodes with conditional execution:

```csharp
[Node("Branch", category: "Flow", description: "Execute different paths based on condition", isCallable: true)]
public void Branch(ExecutionPath Entry, bool Condition, out ExecutionPath True, out ExecutionPath False)
{
    True = new ExecutionPath();
    False = new ExecutionPath();

    if (Condition)
        True.Signal();
    else
        False.Signal();
}
```

**Key points:**
- Multiple output execution paths
- Only signal the paths you want to execute
- Can signal multiple paths for parallel execution

---

## Part 3: Advanced Node Features

### Using Feedback Events

Send messages to the user during execution:

```csharp
[Node("Divide", category: "Math", description: "Divide A by B")]
public void Divide(double A, double B, out double Result)
{
    if (Math.Abs(B) < 0.0001)
    {
        FeedbackWarning?.Invoke(this, new FeedbackEventArgs(
            "Division by zero, returning 0",
            FeedbackType.Warning
        ));
        Result = 0;
        return;
    }
    
    Result = A / B;
}
```

### Working with Complex Types

Define custom types for sockets:

```csharp
public class Vector3
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

public class VectorContext : INodeContext
{
    public event EventHandler<FeedbackEventArgs>? FeedbackInfo;
    public event EventHandler<FeedbackEventArgs>? FeedbackWarning;
    public event EventHandler<FeedbackEventArgs>? FeedbackError;

    [Node("Create Vector", category: "Vector", description: "Create a 3D vector")]
    public void CreateVector(double X, double Y, double Z, out Vector3 Result)
    {
        Result = new Vector3 { X = X, Y = Y, Z = Z };
    }

    [Node("Vector Length", category: "Vector", description: "Get vector magnitude")]
    public void VectorLength(Vector3 Input, out double Length)
    {
        Length = Math.Sqrt(Input.X * Input.X + Input.Y * Input.Y + Input.Z * Input.Z);
    }

    [Node("Add Vectors", category: "Vector", description: "Add two vectors")]
    public void AddVectors(Vector3 A, Vector3 B, out Vector3 Result)
    {
        Result = new Vector3
        {
            X = A.X + B.X,
            Y = A.Y + B.Y,
            Z = A.Z + B.Z
        };
    }
}
```

**Register the type:**

```csharp
@inject SocketTypeResolver TypeResolver

protected override void OnInitialized()
{
    TypeResolver.Register("Vector3", typeof(Vector3));
    TypeResolver.Register(typeof(Vector3).FullName!, typeof(Vector3));
}
```

### Multiple Return Values

Use multiple `out` parameters:

```csharp
[Node("Min Max", category: "Math", description: "Get minimum and maximum")]
public void MinMax(double A, double B, out double Min, out double Max)
{
    Min = Math.Min(A, B);
    Max = Math.Max(A, B);
}
```

### Optional Parameters

Use default values for optional inputs:

```csharp
[Node("Clamp", category: "Math", description: "Clamp value between min and max")]
public void Clamp(double Value, double Min = 0, double Max = 1, out double Result)
{
    Result = Math.Max(Min, Math.Min(Max, Value));
}
```

---

## Part 4: Standard Socket Editors (Attribute-Based)

Use `[SocketEditor]` to select a built-in editor for an input socket. This keeps UIs consistent and avoids custom component code.

```csharp
using NodeEditor.Net.Services.Registry;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Net.Services.Execution;

[Node("Image Loader", category: "Media")]
public void LoadImage(
    [SocketEditor(SocketEditorKind.Image, Label = "Image Path")] string ImagePath,
    [SocketEditor(SocketEditorKind.Dropdown, Options = "PNG,JPEG,BMP")] string Format,
    [SocketEditor(SocketEditorKind.NumberUpDown, Min = 0, Max = 100, Step = 1)] int Quality,
    out ExecutionPath Exit)
{
    Exit = default;
}
```

Notes:
- Enum-typed inputs automatically render as dropdowns without `[SocketEditor]`.
- If an input is connected, editors remain hidden (same as existing behavior).

---

## Part 5: Custom Socket Editors

Create custom UI for editing socket values when built-in editors aren't enough.

### Step 1: Implement INodeCustomEditor

```csharp
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using NodeEditor.Net.Services.Registry;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Blazor.Services.Editors;

namespace MyApp.Editors;

public sealed class ColorEditorDefinition : INodeCustomEditor
{
    public bool CanEdit(SocketData socket)
    {
        return socket.TypeName == "Color" && socket.IsInput && !socket.IsExecution;
    }

    public RenderFragment Render(SocketEditorContext context)
    {
        return builder =>
        {
            var currentValue = context.Socket.Data.Value?.ToObject<string>() ?? "#000000";

            builder.OpenElement(0, "input");
            builder.AddAttribute(1, "type", "color");
            builder.AddAttribute(2, "class", "ne-socket-editor");
            builder.AddAttribute(3, "value", currentValue);
            builder.AddAttribute(4, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(
                this, e => context.SetValue(e.Value?.ToString() ?? "#000000")));
            builder.CloseElement();
        };
    }
}
```

### Step 2: Register the Editor

```csharp
// Program.cs
services.AddSingleton<INodeCustomEditor, ColorEditorDefinition>();
```

### Step 3: Use in Node Context

```csharp
[Node("Set Color", category: "Graphics", description: "Define a color")]
public void SetColor(string Color, out string Result)
{
    Result = Color;
}
```

### Example: Slider Editor (Custom)

```csharp
public sealed class SliderEditorDefinition : INodeCustomEditor
{
    public bool CanEdit(SocketData socket)
    {
        return socket.TypeName == "double" && 
               socket.IsInput && 
               !socket.IsExecution &&
               socket.Name.Contains("Slider");
    }

    public RenderFragment Render(SocketEditorContext context)
    {
        return builder =>
        {
            var currentValue = context.Socket.Data.Value?.ToObject<double>() ?? 0.0;

            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "slider-container");

            // Slider
            builder.OpenElement(2, "input");
            builder.AddAttribute(3, "type", "range");
            builder.AddAttribute(4, "min", "0");
            builder.AddAttribute(5, "max", "100");
            builder.AddAttribute(6, "step", "1");
            builder.AddAttribute(7, "value", currentValue.ToString());
            builder.AddAttribute(8, "oninput", EventCallback.Factory.Create<ChangeEventArgs>(
                this, e =>
                {
                    if (double.TryParse(e.Value?.ToString(), out var val))
                    {
                        context.SetValue(val);
                    }
                }));
            builder.CloseElement();

            // Value display
            builder.OpenElement(9, "span");
            builder.AddAttribute(10, "class", "slider-value");
            builder.AddContent(11, currentValue.ToString("F1"));
            builder.CloseElement();

            builder.CloseElement(); // div
        };
    }
}
```

### Example: Dropdown Editor (Custom)

```csharp
public sealed class EnumEditorDefinition : INodeCustomEditor
{
    public bool CanEdit(SocketData socket)
    {
        return socket.TypeName.StartsWith("Enum:") && socket.IsInput;
    }

    public RenderFragment Render(SocketEditorContext context)
    {
        return builder =>
        {
            // Extract enum values from TypeName (e.g., "Enum:Red,Green,Blue")
            var enumValues = context.Socket.Data.TypeName.Substring(5).Split(',');
            var currentValue = context.Socket.Data.Value?.ToObject<string>() ?? enumValues[0];

            builder.OpenElement(0, "select");
            builder.AddAttribute(1, "class", "ne-socket-editor");
            builder.AddAttribute(2, "value", currentValue);
            builder.AddAttribute(3, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(
                this, e => context.SetValue(e.Value?.ToString() ?? enumValues[0])));

            foreach (var value in enumValues)
            {
                builder.OpenElement(4, "option");
                builder.AddAttribute(5, "value", value);
                builder.AddContent(6, value);
                builder.CloseElement();
            }

            builder.CloseElement();
        };
    }
}
```

---

## Part 6: Creating a Plugin

Package your custom nodes as a reusable plugin.

### Step 1: Create Plugin Project

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\NodeEditor.Blazor\NodeEditor.Blazor.csproj" />
  </ItemGroup>
</Project>
```

### Step 2: Implement INodePlugin

```csharp
using NodeEditor.Net.Services.Registry;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Net.Services.Plugins;
using NodeEditor.Net.Services.Registry;

namespace MyPlugin;

public sealed class ImageProcessingPlugin : INodePlugin
{
    public string Name => "Image Processing Nodes";
    public string Id => "com.example.imageprocessing";
    public Version Version => new(1, 0, 0);
    public Version MinApiVersion => new(1, 0, 0);

    public void Register(NodeRegistryService registry)
    {
        // Register all contexts in this assembly
        registry.RegisterFromAssembly(typeof(ImageProcessingPlugin).Assembly);
    }
}

public class ImageContext : INodeContext
{
    public event EventHandler<FeedbackEventArgs>? FeedbackInfo;
    public event EventHandler<FeedbackEventArgs>? FeedbackWarning;
    public event EventHandler<FeedbackEventArgs>? FeedbackError;

    [Node("Blur", category: "Image", description: "Apply Gaussian blur")]
    public void Blur(string ImagePath, double Radius, out string Result)
    {
        // Implementation
        Result = ImagePath;
        FeedbackInfo?.Invoke(this, new FeedbackEventArgs(
            $"Blurred {ImagePath} with radius {Radius}",
            FeedbackType.Info
        ));
    }

    [Node("Resize", category: "Image", description: "Resize image")]
    public void Resize(string ImagePath, int Width, int Height, out string Result)
    {
        // Implementation
        Result = ImagePath;
    }
}
```

### Step 3: Create plugin.json (optional)

```json
{
  "id": "com.example.imageprocessing",
  "name": "Image Processing Nodes",
  "version": "1.0.0",
  "minApiVersion": "1.0.0",
  "description": "Nodes for image manipulation",
  "author": "Your Name",
  "assembly": "MyPlugin.dll"
}
```

### Step 4: Load the Plugin

**Option A: Dynamic Loading (not supported on iOS)**

```csharp
// Program.cs
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var pluginLoader = scope.ServiceProvider.GetRequiredService<PluginLoader>();
    await pluginLoader.LoadAndRegisterAsync("./plugins");
}
```

**Option B: Direct Registration (iOS-compatible)**

```csharp
// Program.cs
builder.Services.AddNodeEditor();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var registry = scope.ServiceProvider.GetRequiredService<NodeRegistryService>();
    var plugin = new ImageProcessingPlugin();
    plugin.Register(registry);
}
```

---

## Best Practices

### Naming Conventions

✅ **Good:**
```csharp
[Node("Add Numbers", category: "Math", description: "Add two numbers")]
public void AddNumbers(double A, double B, out double Result)
```

❌ **Bad:**
```csharp
[Node("add", category: "math")] // Lowercase, no description
public void add(double a, double b, out double r) // Unclear parameter names
```

### Parameter Names

- Use **PascalCase** for all parameters
- Use **descriptive names**: `Value`, `Result`, `Entry`, `Exit`
- Avoid abbreviations: `Result` not `Res`

### Categories

Organize nodes into logical categories:

```csharp
[Node("Add", category: "Math/Basic")]
[Node("Sin", category: "Math/Trigonometry")]
[Node("Random", category: "Math/Random")]
[Node("Print", category: "Debug/Console")]
[Node("Log", category: "Debug/File")]
```

### Error Handling

Always validate inputs and use feedback events:

```csharp
[Node("Read File", category: "File")]
public void ReadFile(string Path, out string Content)
{
    if (string.IsNullOrWhiteSpace(Path))
    {
        FeedbackError?.Invoke(this, new FeedbackEventArgs(
            "File path cannot be empty",
            FeedbackType.Error
        ));
        Content = string.Empty;
        return;
    }

    if (!File.Exists(Path))
    {
        FeedbackError?.Invoke(this, new FeedbackEventArgs(
            $"File not found: {Path}",
            FeedbackType.Error
        ));
        Content = string.Empty;
        return;
    }

    try
    {
        Content = File.ReadAllText(Path);
        FeedbackInfo?.Invoke(this, new FeedbackEventArgs(
            $"Successfully read {Path}",
            FeedbackType.Info
        ));
    }
    catch (Exception ex)
    {
        FeedbackError?.Invoke(this, new FeedbackEventArgs(
            $"Error reading file: {ex.Message}",
            FeedbackType.Error
        ));
        Content = string.Empty;
    }
}
```

### Type Safety

Register all custom types:

```csharp
// In OnInitialized or Program.cs
TypeResolver.Register("MyCustomType", typeof(MyCustomType));
TypeResolver.Register(typeof(MyCustomType).FullName!, typeof(MyCustomType));
```

### Documentation

Add XML documentation to your context classes:

```csharp
/// <summary>
/// Provides mathematical operations for the node editor.
/// </summary>
public class MathContext : INodeContext
{
    /// <summary>
    /// Adds two numbers together.
    /// </summary>
    /// <param name="A">First number</param>
    /// <param name="B">Second number</param>
    /// <param name="Result">Sum of A and B</param>
    [Node("Add", category: "Math", description: "Add two numbers")]
    public void Add(double A, double B, out double Result)
    {
        Result = A + B;
    }
}
```

### Performance

For heavy computations, consider async operations:

```csharp
[Node("Heavy Computation", category: "Async", isCallable: true)]
public async Task HeavyComputation(ExecutionPath Entry, int Iterations, out ExecutionPath Exit, out int Result)
{
    // Simulate heavy work
    await Task.Delay(100);
    Result = Iterations * 2;
    
    Exit = new ExecutionPath();
    Exit.Signal();
}
```

---

## Complete Example: String Processing Plugin

```csharp
using NodeEditor.Net.Services.Registry;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Net.Services.Plugins;
using NodeEditor.Net.Services.Registry;

namespace StringPlugin;

public sealed class StringPlugin : INodePlugin
{
    public string Name => "String Operations";
    public string Id => "com.example.strings";
    public Version Version => new(1, 0, 0);
    public Version MinApiVersion => new(1, 0, 0);

    public void Register(NodeRegistryService registry)
    {
        registry.RegisterFromAssembly(typeof(StringPlugin).Assembly);
    }
}

public class StringContext : INodeContext
{
    public event EventHandler<FeedbackEventArgs>? FeedbackInfo;
    public event EventHandler<FeedbackEventArgs>? FeedbackWarning;
    public event EventHandler<FeedbackEventArgs>? FeedbackError;

    [Node("Concat", category: "String", description: "Concatenate two strings")]
    public void Concat(string A, string B, out string Result)
    {
        Result = A + B;
    }

    [Node("To Upper", category: "String", description: "Convert to uppercase")]
    public void ToUpper(string Input, out string Result)
    {
        Result = Input.ToUpper();
    }

    [Node("To Lower", category: "String", description: "Convert to lowercase")]
    public void ToLower(string Input, out string Result)
    {
        Result = Input.ToLower();
    }

    [Node("Split", category: "String", description: "Split string by delimiter")]
    public void Split(string Input, string Delimiter, out string FirstPart, out string SecondPart)
    {
        var parts = Input.Split(new[] { Delimiter }, StringSplitOptions.None);
        FirstPart = parts.Length > 0 ? parts[0] : string.Empty;
        SecondPart = parts.Length > 1 ? parts[1] : string.Empty;
    }

    [Node("Length", category: "String", description: "Get string length")]
    public void Length(string Input, out int Length)
    {
        Length = Input.Length;
    }

    [Node("Contains", category: "String", description: "Check if string contains substring")]
    public void Contains(string Input, string Substring, out bool Result)
    {
        Result = Input.Contains(Substring);
    }

    [Node("Replace", category: "String", description: "Replace all occurrences")]
    public void Replace(string Input, string OldValue, string NewValue, out string Result)
    {
        Result = Input.Replace(OldValue, NewValue);
    }
}
```

---

## Next Steps

- Explore the [API Reference](./API.md) for detailed documentation
- Check the [Troubleshooting Guide](./TROUBLESHOOTING.md) for common issues
- Review the [Migration Guide](./MIGRATION.md) if coming from WinForms
- Study sample projects in the repository

Happy node creating! 🎨
