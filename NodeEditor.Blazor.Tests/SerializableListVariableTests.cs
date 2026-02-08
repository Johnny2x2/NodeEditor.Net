using System.Text.Json;
using System.Linq;
using NodeEditor.Net.Models;
using NodeEditor.Net.Services;
using NodeEditor.Net.Services.Infrastructure;
using NodeEditor.Blazor.Services.Editors;
using NodeEditor.Net.Services.Execution;

namespace NodeEditor.Blazor.Tests;

/// <summary>
/// Tests for SerializableList integration with the variables panel:
/// JSON round-trip, SocketValue round-trip, SeedVariables type resolution,
/// editor registry matching, VariableNodeFactory definitions, and
/// graph serialization with list-typed variables.
/// </summary>
public sealed class SerializableListVariableTests
{
    // ════════════════════════════════════════════════════════════════════
    //  1. SerializableListJsonConverter — round-trip
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void JsonConverter_EmptyList_RoundTrips()
    {
        var list = new SerializableList();
        var json = JsonSerializer.Serialize(list);
        var restored = JsonSerializer.Deserialize<SerializableList>(json);

        Assert.NotNull(restored);
        Assert.Equal(0, restored!.Count);
        Assert.Equal("[]", json);
    }

    [Fact]
    public void JsonConverter_ListWithPrimitives_RoundTrips()
    {
        var list = new SerializableList();
        list.Add("hello");
        list.Add(42);
        list.Add(3.14);
        list.Add(true);

        var json = JsonSerializer.Serialize(list);
        var restored = JsonSerializer.Deserialize<SerializableList>(json)!;

        Assert.Equal(4, restored.Count);

        var items = restored.Snapshot();
        Assert.Equal("hello", items[0]);
        Assert.Equal(42, items[1]);
        Assert.Equal(3.14, items[2]);
        Assert.Equal(true, items[3]);
    }

    [Fact]
    public void JsonConverter_NestedList_RoundTrips()
    {
        var inner = new SerializableList();
        inner.Add("a");
        inner.Add("b");

        var outer = new SerializableList();
        outer.Add(inner);
        outer.Add(99);

        var json = JsonSerializer.Serialize(outer);
        var restored = JsonSerializer.Deserialize<SerializableList>(json)!;

        Assert.Equal(2, restored.Count);
        var items = restored.Snapshot();
        Assert.IsType<SerializableList>(items[0]);

        var restoredInner = (SerializableList)items[0];
        Assert.Equal(2, restoredInner.Count);
        Assert.Equal("a", restoredInner.Snapshot()[0]);
        Assert.Equal("b", restoredInner.Snapshot()[1]);
        Assert.Equal(99, items[1]);
    }

    [Fact]
    public void JsonConverter_LegacyEmptyObject_DeserializesAsEmptyList()
    {
        // Before the converter, SerializableList serialized as "{}" — handle gracefully
        var json = "{}";
        var restored = JsonSerializer.Deserialize<SerializableList>(json)!;

        Assert.NotNull(restored);
        Assert.Equal(0, restored.Count);
    }

    [Fact]
    public void JsonConverter_NullItems_Preserved()
    {
        // JSON: [null, "test"]
        var json = "[null, \"test\"]";
        var restored = JsonSerializer.Deserialize<SerializableList>(json)!;

        Assert.Equal(2, restored.Count);
        var items = restored.Snapshot();
        Assert.Null(items[0]);
        Assert.Equal("test", items[1]);
    }

    // ════════════════════════════════════════════════════════════════════
    //  2. SocketValue — SerializableList round-trip
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void SocketValue_FromObject_SerializesListCorrectly()
    {
        var list = new SerializableList();
        list.Add("x");
        list.Add(10);

        var sv = SocketValue.FromObject(list);

        Assert.Equal(typeof(SerializableList).FullName, sv.TypeName);
        Assert.True(sv.Json.HasValue);
        // Should be a JSON array, not an empty object
        Assert.Equal(JsonValueKind.Array, sv.Json!.Value.ValueKind);
    }

    [Fact]
    public void SocketValue_RoundTrips_SerializableList()
    {
        var list = new SerializableList();
        list.Add("alpha");
        list.Add(7);

        var sv = SocketValue.FromObject(list);
        var restored = sv.ToObject<SerializableList>();

        Assert.NotNull(restored);
        Assert.Equal(2, restored!.Count);
        var items = restored.Snapshot();
        Assert.Equal("alpha", items[0]);
        Assert.Equal(7, items[1]);
    }

    [Fact]
    public void SocketValue_EmptyList_RoundTrips()
    {
        var list = new SerializableList();
        var sv = SocketValue.FromObject(list);
        var restored = sv.ToObject<SerializableList>();

        Assert.NotNull(restored);
        Assert.Equal(0, restored!.Count);
    }

    // ════════════════════════════════════════════════════════════════════
    //  3. SeedVariables — type-aware deserialization
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void SeedVariables_WithTypeResolver_SeedsSerializableList()
    {
        var list = new SerializableList();
        list.Add("item1");
        list.Add("item2");

        var variable = new GraphVariable(
            "var1",
            "MyList",
            typeof(SerializableList).FullName!,
            SocketValue.FromObject(list));

        var resolver = new SocketTypeResolver();
        resolver.Register<SerializableList>();

        var context = new NodeExecutionContext();
        VariableNodeExecutor.SeedVariables(context, new[] { variable }, resolver);

        var value = context.GetVariable("var1");
        Assert.NotNull(value);
        Assert.IsType<SerializableList>(value);

        var seeded = (SerializableList)value!;
        Assert.Equal(2, seeded.Count);
        Assert.Equal("item1", seeded.Snapshot()[0]);
        Assert.Equal("item2", seeded.Snapshot()[1]);
    }

    [Fact]
    public void SeedVariables_WithoutTypeResolver_FallsBackToTypeGetType()
    {
        var list = new SerializableList();
        list.Add(42);

        var variable = new GraphVariable(
            "var2",
            "Numbers",
            typeof(SerializableList).FullName!,
            SocketValue.FromObject(list));

        var context = new NodeExecutionContext();
        // No resolver — should still work via Type.GetType fallback since
        // SerializableList has the [JsonConverter] attribute
        VariableNodeExecutor.SeedVariables(context, new[] { variable });

        var value = context.GetVariable("var2");
        // Type.GetType may or may not resolve depending on the assembly,
        // but the converter on the attribute should handle it if it does resolve
        Assert.NotNull(value);
    }

    [Fact]
    public void SeedVariables_NullDefaultValue_SetsNull()
    {
        var variable = new GraphVariable(
            "var3",
            "Empty",
            typeof(SerializableList).FullName!,
            null);

        var context = new NodeExecutionContext();
        VariableNodeExecutor.SeedVariables(context, new[] { variable });

        Assert.Null(context.GetVariable("var3"));
    }

    [Fact]
    public void SeedVariables_MixedTypes_SeedsAll()
    {
        var list = new SerializableList();
        list.Add("test");

        var listVar = new GraphVariable(
            "v1", "Items",
            typeof(SerializableList).FullName!,
            SocketValue.FromObject(list));

        var intVar = new GraphVariable(
            "v2", "Counter",
            typeof(int).FullName!,
            SocketValue.FromObject(42));

        var strVar = new GraphVariable(
            "v3", "Name",
            typeof(string).FullName!,
            SocketValue.FromObject("hello"));

        var resolver = new SocketTypeResolver();
        resolver.Register<SerializableList>();

        var context = new NodeExecutionContext();
        VariableNodeExecutor.SeedVariables(context, new[] { listVar, intVar, strVar }, resolver);

        Assert.IsType<SerializableList>(context.GetVariable("v1"));
        Assert.Equal(42, context.GetVariable("v2"));
        Assert.Equal("hello", context.GetVariable("v3"));
    }

    // ════════════════════════════════════════════════════════════════════
    //  4. SocketTypeResolver — registration
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void SocketTypeResolver_ResolveSerializableList()
    {
        var resolver = new SocketTypeResolver();
        resolver.Register<SerializableList>();

        var resolved = resolver.Resolve(typeof(SerializableList).FullName);

        Assert.NotNull(resolved);
        Assert.Equal(typeof(SerializableList), resolved);
    }

    // ════════════════════════════════════════════════════════════════════
    //  5. EditorRegistry — List editor selection
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void EditorRegistry_SelectsListEditorForSerializableList()
    {
        var registry = new NodeEditorCustomEditorRegistry(new INodeCustomEditor[]
        {
            new TextEditorDefinition(),
            new NumericEditorDefinition(),
            new BoolEditorDefinition(),
            new ListEditorDefinition()
        });

        var socket = new SocketData("Items",
            typeof(SerializableList).FullName ?? "NodeEditor.Net.Models.SerializableList",
            true, false);

        var editor = registry.GetEditor(socket);

        Assert.NotNull(editor);
        Assert.IsType<ListEditorDefinition>(editor);
    }

    [Fact]
    public void EditorRegistry_ListEditorDoesNotMatchOtherTypes()
    {
        var editor = new ListEditorDefinition();

        Assert.False(editor.CanEdit(new SocketData("V", "System.String", true, false)));
        Assert.False(editor.CanEdit(new SocketData("V", "System.Int32", true, false)));
        Assert.False(editor.CanEdit(new SocketData("V", "System.Boolean", true, false)));
        Assert.False(editor.CanEdit(new SocketData("V", "System.Object", true, false)));
    }

    [Fact]
    public void EditorRegistry_ListEditorDoesNotMatchOutputSockets()
    {
        var editor = new ListEditorDefinition();

        // Output socket (IsInput = false)
        Assert.False(editor.CanEdit(new SocketData("Items",
            typeof(SerializableList).FullName!, false, false)));
    }

    [Fact]
    public void EditorRegistry_ListEditorDoesNotMatchExecutionSockets()
    {
        var editor = new ListEditorDefinition();

        // Execution socket
        Assert.False(editor.CanEdit(new SocketData("Enter",
            typeof(SerializableList).FullName!, true, true)));
    }

    // ════════════════════════════════════════════════════════════════════
    //  6. VariableNodeFactory — builds correct definitions for List vars
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void VariablesPanel_GetDefaultValueForListType_ReturnsEmptyList()
    {
        // Mirrors the VariablesPanel.GetDefaultValueForType switch
        var typeName = typeof(SerializableList).FullName!;
        var sv = SocketValue.FromObject(new SerializableList());

        Assert.NotNull(sv);
        Assert.Equal(typeName, sv.TypeName);

        var restored = sv.ToObject<SerializableList>();
        Assert.NotNull(restored);
        Assert.Equal(0, restored!.Count);
    }

    [Fact]
    public void VariableNodeFactory_RegistersGetAndSetForListVariable()
    {
        var state = new NodeEditorState();
        var registry = new NodeEditor.Net.Services.Registry.NodeRegistryService(
            new NodeEditor.Net.Services.Registry.NodeDiscoveryService());
        registry.EnsureInitialized(Array.Empty<System.Reflection.Assembly>());

        var factory = new VariableNodeFactory(registry, state);

        var variable = GraphVariable.Create(
            "MyList",
            typeof(SerializableList).FullName!,
            SocketValue.FromObject(new SerializableList()));

        state.AddVariable(variable);

        // Verify Get definition was created
        var getDef = registry.Definitions.FirstOrDefault(d => d.Id == variable.GetDefinitionId);
        Assert.NotNull(getDef);
        Assert.Equal("Get MyList", getDef!.Name);
        Assert.Equal("Variables", getDef.Category);
        Assert.Empty(getDef.Inputs);
        Assert.Single(getDef.Outputs);
        Assert.Equal(typeof(SerializableList).FullName, getDef.Outputs[0].TypeName);

        // Verify Set definition was created
        var setDef = registry.Definitions.FirstOrDefault(d => d.Id == variable.SetDefinitionId);
        Assert.NotNull(setDef);
        Assert.Equal("Set MyList", setDef!.Name);
        Assert.Equal("Variables", setDef.Category);
        // Set node: Enter (exec), Value (data input)
        Assert.Equal(2, setDef.Inputs.Count);
        Assert.Contains(setDef.Inputs, s => s.Name == "Value" && s.TypeName == typeof(SerializableList).FullName);
    }

    [Fact]
    public void VariableNodeFactory_UpdatesDefinitionsOnTypeChange()
    {
        var state = new NodeEditorState();
        var registry = new NodeEditor.Net.Services.Registry.NodeRegistryService(
            new NodeEditor.Net.Services.Registry.NodeDiscoveryService());
        registry.EnsureInitialized(Array.Empty<System.Reflection.Assembly>());

        var factory = new VariableNodeFactory(registry, state);

        // Start as int
        var variable = GraphVariable.Create("Counter", typeof(int).FullName!, SocketValue.FromObject(0));
        state.AddVariable(variable);

        var getDef = registry.Definitions.FirstOrDefault(d => d.Id == variable.GetDefinitionId);
        Assert.NotNull(getDef);
        Assert.Equal(typeof(int).FullName, getDef!.Outputs[0].TypeName);

        // Change to list
        var updated = variable with
        {
            TypeName = typeof(SerializableList).FullName!,
            DefaultValue = SocketValue.FromObject(new SerializableList())
        };
        state.UpdateVariable(updated);

        var updatedDef = registry.Definitions.FirstOrDefault(d => d.Id == updated.GetDefinitionId);
        Assert.NotNull(updatedDef);
        Assert.Equal(typeof(SerializableList).FullName, updatedDef!.Outputs[0].TypeName);
    }

    // ════════════════════════════════════════════════════════════════════
    //  7. VariableNodeExecutor — Get/Set execute with SerializableList
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void VariableNodeExecutor_GetNode_ReturnsSeededList()
    {
        var variable = GraphVariable.Create("Items", typeof(SerializableList).FullName!);
        var list = new SerializableList();
        list.Add("a");
        list.Add("b");

        var context = new NodeExecutionContext();
        context.SetVariable(variable.Id, list);

        var getNode = new NodeData(
            Id: "get-1",
            Name: "Get Items",
            Callable: false,
            ExecInit: false,
            Inputs: Array.Empty<SocketData>(),
            Outputs: new[] { new SocketData("Value", typeof(SerializableList).FullName!, false, false) },
            DefinitionId: variable.GetDefinitionId);

        VariableNodeExecutor.Execute(getNode, context);

        var result = context.GetSocketValue("get-1", "Value");
        Assert.IsType<SerializableList>(result);
        Assert.Equal(2, ((SerializableList)result!).Count);
    }

    [Fact]
    public void VariableNodeExecutor_SetNode_UpdatesVariable()
    {
        var variable = GraphVariable.Create("Items", typeof(SerializableList).FullName!);
        var context = new NodeExecutionContext();
        context.SetVariable(variable.Id, new SerializableList());

        var newList = new SerializableList();
        newList.Add("updated");

        // Simulate input value already set on the socket
        context.SetSocketValue("set-1", "Value", newList);

        var setNode = new NodeData(
            Id: "set-1",
            Name: "Set Items",
            Callable: true,
            ExecInit: false,
            Inputs: new[]
            {
                new SocketData("Enter", typeof(ExecutionPath).FullName!, true, true),
                new SocketData("Value", typeof(SerializableList).FullName!, true, false)
            },
            Outputs: new[]
            {
                new SocketData("Exit", typeof(ExecutionPath).FullName!, false, true),
                new SocketData("Value", typeof(SerializableList).FullName!, false, false)
            },
            DefinitionId: variable.SetDefinitionId);

        VariableNodeExecutor.Execute(setNode, context);

        var stored = context.GetVariable(variable.Id);
        Assert.IsType<SerializableList>(stored);
        Assert.Equal(1, ((SerializableList)stored!).Count);
        Assert.Equal("updated", ((SerializableList)stored).Snapshot()[0]);
    }

    // ════════════════════════════════════════════════════════════════════
    //  8. Graph serialization — round-trip with list variables
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public void GraphSerializer_RoundTrips_ListVariable()
    {
        var state = new NodeEditorState();

        var list = new SerializableList();
        list.Add("one");
        list.Add(2);

        var variable = GraphVariable.Create(
            "MyList",
            typeof(SerializableList).FullName!,
            SocketValue.FromObject(list));

        state.AddVariable(variable);

        var serializer = CreateSerializer();
        var dto = serializer.Export(state);
        var json = serializer.Serialize(dto);

        // Verify JSON contains the list as an array, not "{}"
        Assert.Contains("[", json);

        var reimported = serializer.Deserialize(json);
        var newState = new NodeEditorState();
        var result = serializer.Import(newState, reimported);

        Assert.Empty(result.Warnings);
        Assert.Single(newState.Variables);

        var restored = newState.Variables[0];
        Assert.Equal(variable.Name, restored.Name);
        Assert.Equal(typeof(SerializableList).FullName, restored.TypeName);

        var restoredList = restored.DefaultValue?.ToObject<SerializableList>();
        Assert.NotNull(restoredList);
        Assert.Equal(2, restoredList!.Count);
        Assert.Equal("one", restoredList.Snapshot()[0]);
    }

    // ════════════════════════════════════════════════════════════════════
    //  9. Helper display methods (from VariablesPanel)
    // ════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("NodeEditor.Net.Models.SerializableList", "List")]
    [InlineData("System.Double", "Num")]
    [InlineData("System.Int32", "Int")]
    [InlineData("System.String", "Str")]
    [InlineData("System.Boolean", "Bool")]
    [InlineData("System.Object", "Obj")]
    public void GetTypeFriendlyName_ReturnsExpected(string typeName, string expected)
    {
        var result = GetTypeFriendlyName(typeName);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("NodeEditor.Net.Models.SerializableList", "list")]
    [InlineData("System.Double", "number")]
    [InlineData("System.Int32", "int")]
    [InlineData("System.String", "string")]
    [InlineData("System.Boolean", "bool")]
    [InlineData("System.Object", "object")]
    public void GetTypeClass_ReturnsExpected(string typeName, string expected)
    {
        var result = GetTypeClass(typeName);
        Assert.Equal(expected, result);
    }

    // Mirrors VariablesPanel helper methods
    private static string GetTypeFriendlyName(string typeName) => typeName switch
    {
        "System.Double" => "Num",
        "System.Int32" => "Int",
        "System.String" => "Str",
        "System.Boolean" => "Bool",
        "NodeEditor.Net.Models.SerializableList" => "List",
        "System.Object" => "Obj",
        _ => typeName.Split('.').Last()
    };

    private static string GetTypeClass(string typeName) => typeName switch
    {
        "System.Double" => "number",
        "System.Int32" => "int",
        "System.String" => "string",
        "System.Boolean" => "bool",
        "NodeEditor.Net.Models.SerializableList" => "list",
        "System.Object" => "object",
        _ => "unknown"
    };

    // ════════════════════════════════════════════════════════════════════
    //  Private helpers
    // ════════════════════════════════════════════════════════════════════

    private static NodeEditor.Net.Services.Serialization.GraphSerializer CreateSerializer()
    {
        var registry = new NodeEditor.Net.Services.Registry.NodeRegistryService(
            new NodeEditor.Net.Services.Registry.NodeDiscoveryService());
        registry.EnsureInitialized(Array.Empty<System.Reflection.Assembly>());

        var resolver = new SocketTypeResolver();
        resolver.Register<SerializableList>();
        var validator = new ConnectionValidator(resolver);
        var migrator = new NodeEditor.Net.Services.Serialization.GraphSchemaMigrator();

        return new NodeEditor.Net.Services.Serialization.GraphSerializer(registry, validator, migrator);
    }
}
