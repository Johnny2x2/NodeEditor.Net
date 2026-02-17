using Bunit;
using NodeEditor.Blazor.Components.Editors;
using NodeEditor.Net.Models;
using NodeEditor.Blazor.Services.Editors;
using NodeEditor.Net.ViewModels;

namespace NodeEditor.Blazor.Tests;

public sealed class EditorComponentTests : BunitContext
{
    [Fact]
    public void TextEditor_InputUpdatesSocketValue()
    {
        var socketVm = new SocketViewModel(new SocketData("Text", typeof(string).FullName ?? "System.String", true, false));
        var nodeVm = new NodeViewModel(new NodeData("n1", "Node", false, false, Array.Empty<SocketData>(), Array.Empty<SocketData>()));
        var context = new SocketEditorContext
        {
            Socket = socketVm,
            Node = nodeVm,
            SetValue = socketVm.SetValue
        };

        var cut = Render<TextEditor>(parameters => parameters.Add(p => p.Context, context));
        cut.Find("input").Input("hello");

        Assert.Equal("hello", socketVm.Data.Value?.ToObject<string>());
    }

    [Fact]
    public void NumericEditor_InputUpdatesSocketValue()
    {
        var socketVm = new SocketViewModel(new SocketData("Value", "int", true, false));
        var nodeVm = new NodeViewModel(new NodeData("n1", "Node", false, false, Array.Empty<SocketData>(), Array.Empty<SocketData>()));
        var context = new SocketEditorContext
        {
            Socket = socketVm,
            Node = nodeVm,
            SetValue = socketVm.SetValue
        };

        var cut = Render<NumericEditor>(parameters => parameters.Add(p => p.Context, context));
        cut.Find("input").Input("123");

        Assert.Equal(123, socketVm.Data.Value?.ToObject<int>());
    }

    [Fact]
    public void BoolEditor_CheckboxUpdatesSocketValue()
    {
        var socketVm = new SocketViewModel(new SocketData("Flag", "bool", true, false));
        var nodeVm = new NodeViewModel(new NodeData("n1", "Node", false, false, Array.Empty<SocketData>(), Array.Empty<SocketData>()));
        var context = new SocketEditorContext
        {
            Socket = socketVm,
            Node = nodeVm,
            SetValue = socketVm.SetValue
        };

        var cut = Render<BoolEditor>(parameters => parameters.Add(p => p.Context, context));
        cut.Find("input").Change(true);

        Assert.True(socketVm.Data.Value?.ToObject<bool>());
    }
}
