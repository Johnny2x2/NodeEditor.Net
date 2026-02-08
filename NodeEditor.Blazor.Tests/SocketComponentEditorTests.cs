using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Blazor.Components;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services.Editors;
using NodeEditor.Blazor.ViewModels;

namespace NodeEditor.Blazor.Tests;

public sealed class SocketComponentEditorTests : BunitContext
{
    public SocketComponentEditorTests()
    {
        Services.AddSingleton<INodeCustomEditor, TextEditorDefinition>();
        Services.AddSingleton<INodeCustomEditor, NumericEditorDefinition>();
        Services.AddSingleton<INodeCustomEditor, BoolEditorDefinition>();
        Services.AddSingleton<NodeEditorCustomEditorRegistry>();
    }

    [Fact]
    public void SocketComponent_RendersEditorWhenInputNotConnected()
    {
        var socketVm = new SocketViewModel(new SocketData("Text", "string", true, false));
        var nodeVm = new NodeViewModel(new NodeData("n1", "Node", false, false, Array.Empty<SocketData>(), Array.Empty<SocketData>()));

        var cut = Render<SocketComponent>(parameters => parameters
            .Add(p => p.Socket, socketVm)
            .Add(p => p.NodeId, "n1")
            .Add(p => p.Node, nodeVm)
            .Add(p => p.IsConnected, false));

        Assert.NotNull(cut.Find(".ne-editor-text"));
    }

    [Fact]
    public void SocketComponent_HidesEditorWhenInputConnected()
    {
        var socketVm = new SocketViewModel(new SocketData("Text", "string", true, false));
        var nodeVm = new NodeViewModel(new NodeData("n1", "Node", false, false, Array.Empty<SocketData>(), Array.Empty<SocketData>()));

        var cut = Render<SocketComponent>(parameters => parameters
            .Add(p => p.Socket, socketVm)
            .Add(p => p.NodeId, "n1")
            .Add(p => p.Node, nodeVm)
            .Add(p => p.IsConnected, true));

        Assert.Empty(cut.FindAll(".ne-editor-text"));
    }
}
