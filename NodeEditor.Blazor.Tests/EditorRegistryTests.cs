using NodeEditor.Net.Models;
using NodeEditor.Blazor.Services.Editors;
using NodeEditor.Net.ViewModels;

namespace NodeEditor.Blazor.Tests;

public sealed class EditorRegistryTests
{
    [Fact]
    public void EditorRegistry_SelectsTextEditorForString()
    {
        var registry = new NodeEditorCustomEditorRegistry(new INodeCustomEditor[]
        {
            new TextEditorDefinition(),
            new NumericEditorDefinition(),
            new BoolEditorDefinition()
        });

        var socket = new SocketData("Name", typeof(string).FullName ?? "System.String", true, false);
        var editor = registry.GetEditor(socket);

        Assert.IsType<TextEditorDefinition>(editor);
    }

    [Fact]
    public void EditorRegistry_SelectsNumericEditorForInt()
    {
        var registry = new NodeEditorCustomEditorRegistry(new INodeCustomEditor[]
        {
            new TextEditorDefinition(),
            new NumericEditorDefinition(),
            new BoolEditorDefinition()
        });

        var socket = new SocketData("Value", "int", true, false);
        var editor = registry.GetEditor(socket);

        Assert.IsType<NumericEditorDefinition>(editor);
    }

    [Fact]
    public void EditorRegistry_SelectsBoolEditorForBool()
    {
        var registry = new NodeEditorCustomEditorRegistry(new INodeCustomEditor[]
        {
            new TextEditorDefinition(),
            new NumericEditorDefinition(),
            new BoolEditorDefinition()
        });

        var socket = new SocketData("Flag", "bool", true, false);
        var editor = registry.GetEditor(socket);

        Assert.IsType<BoolEditorDefinition>(editor);
    }

    [Fact]
    public void SocketViewModel_SetValue_UpdatesSocketData()
    {
        var socket = new SocketViewModel(new SocketData("Value", "int", true, false));

        socket.SetValue(42);

        Assert.NotNull(socket.Data.Value);
        Assert.Equal(42, socket.Data.Value?.ToObject<int>());
    }
}
