# Custom Nodes Tutorial

Learn how to create custom nodes and custom socket editors for NodeEditor.Blazor.

## Table of Contents

- [Overview](#overview)
- [Part 1: Creating Your First Custom Node](#part-1-creating-your-first-custom-node)
- [Part 2: Working with Execution Flow](#part-2-working-with-execution-flow)
- [Part 3: Advanced Node Features](#part-3-advanced-node-features)
- [Part 4: Socket Editor Hints](#part-4-socket-editor-hints)
- [Part 5: Custom Socket Editors](#part-5-custom-socket-editors)
- [Part 6: Creating a Plugin](#part-6-creating-a-plugin)
- [Best Practices](#best-practices)

---

## Overview

Custom nodes allow you to extend the node editor with domain-specific functionality. Nodes are defined by subclassing `NodeBase`, overriding `Configure` (to declare sockets via the fluent builder API) and `ExecuteAsync` (to implement logic).

### Node Types

1. **Data Nodes** - Process inputs and produce outputs (no execution flow)
2. **Executable Nodes** - Have execution flow control
3. **Entry Nodes** - Start execution automatically
4. **Branching Nodes** - Conditional execution paths

---

## Part 1: Creating Your First Custom Node

### Step 1: Create Node Classes

```csharp
using NodeEditor.Net.Services.Execution;

namespace MyApp.Nodes;

public sealed class AddNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Add").Category("Math")
            .Description("Add two numbers")
            .Input<double>("A", 0.0)
            .Input<double>("B", 0.0)
            .Output<double>("Result");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        context.SetOutput("Result", context.GetInput<double>("A") + context.GetInput<double>("B"));
        return Task.CompletedTask;
    }
}

public sealed class MultiplyNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Multiply").Category("Math")
            .Description("Multiply two numbers")
            .Input<double>("A", 0.0)
            .Input<double>("B", 0.0)
            .Output<double>("Result");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        context.SetOutput("Result", context.GetInput<double>("A") * context.GetInput<double>("B"));
        return Task.CompletedTask;
    }
}

public sealed class PowerNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Power").Category("Math")
            .Description("Raise A to the power of B")
            .Input<double>("A", 0.0)
            .Input<double>("B", 2.0)
            .Output<double>("Result");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        context.SetOutput("Result", Math.Pow(context.GetInput<double>("A"), context.GetInput<double>("B")));
        return Task.CompletedTask;
    }
}
```

### Step 2: Register the Nodes

```razor
@inject INodeRegistryService Registry

@code {
    protected override void OnInitialized()
    {
        // Register all NodeBase subclasses from your assembly
        Registry.RegisterFromAssembly(typeof(AddNode).Assembly);
    }
}
```

### Step 3: Nodes Appear Automatically

Right-click on the canvas → "Math" category → Select "Add", "Multiply", or "Power"

---

## Part 2: Working with Execution Flow

### Executable Nodes

Nodes with execution flow use `Callable()` on the builder, which adds `Enter` and `Exit` execution sockets:

```csharp
public sealed class PrintNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Print").Category("Debug")
            .Description("Print value to console")
            .Callable()
            .Input<string>("Message", "");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var message = context.GetInput<string>("Message");
        context.EmitFeedback(message, ExecutionFeedbackType.DebugPrint);
        await context.TriggerAsync("Exit");
    }
}
```

**Key points:**
- Call `Callable()` to add `Enter` and `Exit` execution sockets
- Use `context.TriggerAsync("Exit")` to continue execution flow
- Use `context.EmitFeedback()` for user-visible messages

### Entry Nodes

Entry nodes start execution automatically:

```csharp
public sealed class OnStartNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("On Start").Category("Events")
            .Description("Executes when the graph starts")
            .ExecutionInitiator();
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        context.EmitFeedback("Graph started", ExecutionFeedbackType.Info);
        await context.TriggerAsync("Exit");
    }
}
```

**Key points:**
- Call `ExecutionInitiator()` for entry nodes (adds `Exit` output, no input)
- Always trigger at least one output execution path

### Branching Nodes

Nodes with conditional execution use named execution inputs/outputs:

```csharp
public sealed class BranchNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Branch").Category("Flow")
            .Description("Execute different paths based on condition")
            .ExecutionInput("Start")
            .Input<bool>("Condition")
            .ExecutionOutput("True")
            .ExecutionOutput("False");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var cond = context.GetInput<bool>("Condition");
        await context.TriggerAsync(cond ? "True" : "False");
    }
}
```

**Key points:**
- Use `ExecutionInput()` / `ExecutionOutput()` for named execution sockets
- Trigger only the paths you want to execute
- Can trigger multiple paths for parallel execution

---

## Part 3: Advanced Node Features

### Using Feedback

Send messages to the user during execution:

```csharp
public sealed class DivideNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Divide").Category("Math")
            .Description("Divide A by B")
            .Input<double>("A", 0.0)
            .Input<double>("B", 1.0)
            .Output<double>("Result");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var b = context.GetInput<double>("B");
        if (Math.Abs(b) < 0.0001)
        {
            context.EmitFeedback("Division by zero, returning 0", ExecutionFeedbackType.Warning);
            context.SetOutput("Result", 0.0);
        }
        else
        {
            context.SetOutput("Result", context.GetInput<double>("A") / b);
        }
        return Task.CompletedTask;
    }
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

public class CreateVectorNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Create Vector")
               .Category("Vector")
               .Description("Create a 3D vector")
               .Input<double>("X", 0.0)
               .Input<double>("Y", 0.0)
               .Input<double>("Z", 0.0)
               .Output<Vector3>("Result");
    }

    public override Task ExecuteAsync(
        INodeExecutionContext context, CancellationToken ct)
    {
        context.SetOutput("Result", new Vector3
        {
            X = context.GetInput<double>("X"),
            Y = context.GetInput<double>("Y"),
            Z = context.GetInput<double>("Z")
        });
        return Task.CompletedTask;
    }
}

public class VectorLengthNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Vector Length")
               .Category("Vector")
               .Description("Get vector magnitude")
               .Input<Vector3>("Input")
               .Output<double>("Length");
    }

    public override Task ExecuteAsync(
        INodeExecutionContext context, CancellationToken ct)
    {
        var v = context.GetInput<Vector3>("Input");
        context.SetOutput("Length",
            Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z));
        return Task.CompletedTask;
    }
}

public class AddVectorsNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Add Vectors")
               .Category("Vector")
               .Description("Add two vectors")
               .Input<Vector3>("A")
               .Input<Vector3>("B")
               .Output<Vector3>("Result");
    }

    public override Task ExecuteAsync(
        INodeExecutionContext context, CancellationToken ct)
    {
        var a = context.GetInput<Vector3>("A");
        var b = context.GetInput<Vector3>("B");
        context.SetOutput("Result", new Vector3
        {
            X = a.X + b.X,
            Y = a.Y + b.Y,
            Z = a.Z + b.Z
        });
        return Task.CompletedTask;
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

### Multiple Outputs

Use multiple `Output<T>()` calls:

```csharp
public sealed class MinMaxNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Min Max").Category("Math")
            .Description("Get minimum and maximum")
            .Input<double>("A", 0.0)
            .Input<double>("B", 0.0)
            .Output<double>("Min")
            .Output<double>("Max");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var a = context.GetInput<double>("A");
        var b = context.GetInput<double>("B");
        context.SetOutput("Min", Math.Min(a, b));
        context.SetOutput("Max", Math.Max(a, b));
        return Task.CompletedTask;
    }
}
```

### Default Values

Provide defaults in the `Input<T>()` call:

```csharp
public sealed class ClampNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Clamp").Category("Math")
            .Description("Clamp value between min and max")
            .Input<double>("Value", 0.0)
            .Input<double>("Min", 0.0)
            .Input<double>("Max", 1.0)
            .Output<double>("Result");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var value = context.GetInput<double>("Value");
        var min = context.GetInput<double>("Min");
        var max = context.GetInput<double>("Max");
        context.SetOutput("Result", Math.Max(min, Math.Min(max, value)));
        return Task.CompletedTask;
    }
}
```

---

## Part 4: Socket Editor Hints

Use `SocketEditorHint` to select a built-in editor for an input socket. Pass it as the `editorHint` parameter in the builder's `Input` call.

```csharp
using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Execution;

public sealed class ImageLoaderNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Image Loader").Category("Media")
            .Callable()
            .Input<string>("ImagePath", "",
                editorHint: new SocketEditorHint(SocketEditorKind.Image, Label: "Image Path"))
            .Input<string>("Format", "PNG",
                editorHint: new SocketEditorHint(SocketEditorKind.Dropdown, Options: "PNG,JPEG,BMP"))
            .Input<int>("Quality", 80,
                editorHint: new SocketEditorHint(SocketEditorKind.NumberUpDown, Min: 0, Max: 100, Step: 1));
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        // ...
        await context.TriggerAsync("Exit");
    }
}
```

Notes:
- Enum-typed inputs automatically render as dropdowns without an explicit hint.
- If an input is connected, editors remain hidden (same as existing behavior).

---

## Part 5: Custom Socket Editors

Create custom UI for editing socket values when built-in editors aren't enough.

### Step 1: Implement INodeCustomEditor

```csharp
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using NodeEditor.Net.Models;
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

### Step 3: Use in a Node

```csharp
public sealed class SetColorNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Set Color").Category("Graphics")
            .Description("Define a color")
            .Input<string>("Color", "#000000")
            .Output<string>("Result");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        context.SetOutput("Result", context.GetInput<string>("Color"));
        return Task.CompletedTask;
    }
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

namespace MyPlugin;

public sealed class ImageProcessingPlugin : INodePlugin
{
    public string Name => "Image Processing Nodes";
    public string Id => "com.example.imageprocessing";
    public Version Version => new(1, 0, 0);
    public Version MinApiVersion => new(1, 0, 0);

    public void Register(INodeRegistryService registry)
    {
        // Register all NodeBase subclasses in this assembly
        registry.RegisterFromAssembly(typeof(ImageProcessingPlugin).Assembly);
    }
}

public sealed class BlurNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Blur").Category("Image")
            .Description("Apply Gaussian blur")
            .Callable()
            .Input<string>("ImagePath", "")
            .Input<double>("Radius", 5.0)
            .Output<string>("Result");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var path = context.GetInput<string>("ImagePath");
        var radius = context.GetInput<double>("Radius");
        // Implementation
        context.SetOutput("Result", path);
        context.EmitFeedback($"Blurred {path} with radius {radius}", ExecutionFeedbackType.Info);
        await context.TriggerAsync("Exit");
    }
}

public sealed class ResizeNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Resize").Category("Image")
            .Description("Resize image")
            .Callable()
            .Input<string>("ImagePath", "")
            .Input<int>("Width", 800)
            .Input<int>("Height", 600)
            .Output<string>("Result");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        // Implementation
        context.SetOutput("Result", context.GetInput<string>("ImagePath"));
        await context.TriggerAsync("Exit");
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
    var registry = scope.ServiceProvider.GetRequiredService<INodeRegistryService>();
    var plugin = new ImageProcessingPlugin();
    plugin.Register(registry);
}
```

---

## Best Practices

### Naming Conventions

✅ **Good:**
```csharp
builder.Name("Add Numbers").Category("Math")
    .Description("Add two numbers")
    .Input<double>("A", 0.0).Input<double>("B", 0.0)
    .Output<double>("Result");
```

❌ **Bad:**
```csharp
builder.Name("add").Category("math"); // Lowercase, no description
// Unclear socket names like "a", "b", "r"
```

### Socket Names

- Use **PascalCase** for all socket names
- Use **descriptive names**: `Value`, `Result`, `Enter`, `Exit`
- Avoid abbreviations: `Result` not `Res`

### Categories

Organize nodes into logical categories:

```csharp
builder.Name("Add").Category("Math/Basic");
builder.Name("Sin").Category("Math/Trigonometry");
builder.Name("Random").Category("Math/Random");
builder.Name("Print").Category("Debug/Console");
builder.Name("Log").Category("Debug/File");
```

### Error Handling

Always validate inputs and use feedback:

```csharp
public sealed class ReadFileNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Read File").Category("File")
            .Callable()
            .Input<string>("Path", "")
            .Output<string>("Content");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var path = context.GetInput<string>("Path");

        if (string.IsNullOrWhiteSpace(path))
        {
            context.EmitFeedback("File path cannot be empty", ExecutionFeedbackType.Error);
            context.SetOutput("Content", string.Empty);
            await context.TriggerAsync("Exit");
            return;
        }

        if (!File.Exists(path))
        {
            context.EmitFeedback($"File not found: {path}", ExecutionFeedbackType.Error);
            context.SetOutput("Content", string.Empty);
            await context.TriggerAsync("Exit");
            return;
        }

        try
        {
            var content = File.ReadAllText(path);
            context.SetOutput("Content", content);
            context.EmitFeedback($"Successfully read {path}", ExecutionFeedbackType.Info);
        }
        catch (Exception ex)
        {
            context.EmitFeedback($"Error reading file: {ex.Message}", ExecutionFeedbackType.Error);
            context.SetOutput("Content", string.Empty);
        }

        await context.TriggerAsync("Exit");
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

Add XML documentation to your node classes:

```csharp
/// <summary>
/// Adds two numbers together.
/// </summary>
public sealed class AddNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Add").Category("Math")
            .Description("Add two numbers")
            .Input<double>("A", 0.0)
            .Input<double>("B", 0.0)
            .Output<double>("Result");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        context.SetOutput("Result", context.GetInput<double>("A") + context.GetInput<double>("B"));
        return Task.CompletedTask;
    }
}
```
```

### Performance

Nodes are inherently async — use `await` for heavy work:

```csharp
public sealed class HeavyComputationNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Heavy Computation").Category("Async")
            .Callable()
            .Input<int>("Iterations", 10)
            .Output<int>("Result");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var iterations = context.GetInput<int>("Iterations");
        // Simulate heavy work
        await Task.Delay(100, ct);
        context.SetOutput("Result", iterations * 2);
        await context.TriggerAsync("Exit");
    }
}
```

---

## Complete Example: String Processing Plugin

```csharp
using NodeEditor.Net.Services.Registry;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Net.Services.Plugins;

namespace StringPlugin;

public sealed class StringPlugin : INodePlugin
{
    public string Name => "String Operations";
    public string Id => "com.example.strings";
    public Version Version => new(1, 0, 0);
    public Version MinApiVersion => new(1, 0, 0);

    public void Register(INodeRegistryService registry)
    {
        registry.RegisterFromAssembly(typeof(StringPlugin).Assembly);
    }
}

public sealed class ConcatNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Concat").Category("String")
            .Description("Concatenate two strings")
            .Input<string>("A", "").Input<string>("B", "")
            .Output<string>("Result");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        context.SetOutput("Result", context.GetInput<string>("A") + context.GetInput<string>("B"));
        return Task.CompletedTask;
    }
}

public sealed class ToUpperNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("To Upper").Category("String")
            .Description("Convert to uppercase")
            .Input<string>("Input", "")
            .Output<string>("Result");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        context.SetOutput("Result", context.GetInput<string>("Input").ToUpper());
        return Task.CompletedTask;
    }
}

public sealed class ToLowerNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("To Lower").Category("String")
            .Description("Convert to lowercase")
            .Input<string>("Input", "")
            .Output<string>("Result");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        context.SetOutput("Result", context.GetInput<string>("Input").ToLower());
        return Task.CompletedTask;
    }
}

public sealed class StringLengthNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Length").Category("String")
            .Description("Get string length")
            .Input<string>("Input", "")
            .Output<int>("Length");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        context.SetOutput("Length", context.GetInput<string>("Input").Length);
        return Task.CompletedTask;
    }
}

public sealed class StringContainsNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Contains").Category("String")
            .Description("Check if string contains substring")
            .Input<string>("Input", "").Input<string>("Substring", "")
            .Output<bool>("Result");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        context.SetOutput("Result",
            context.GetInput<string>("Input").Contains(context.GetInput<string>("Substring")));
        return Task.CompletedTask;
    }
}

public sealed class StringReplaceNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Replace").Category("String")
            .Description("Replace all occurrences")
            .Input<string>("Input", "").Input<string>("OldValue", "").Input<string>("NewValue", "")
            .Output<string>("Result");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        context.SetOutput("Result",
            context.GetInput<string>("Input").Replace(
                context.GetInput<string>("OldValue"),
                context.GetInput<string>("NewValue")));
        return Task.CompletedTask;
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
