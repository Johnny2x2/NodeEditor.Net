using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services;
using NodeEditor.Blazor.Services.Plugins;
using NodeEditor.Blazor.ViewModels;

namespace NodeEditor.Blazor.Tests;

public sealed class PluginEventBusTests
{
    [Fact]
    public void PluginEventBus_PublishesNodeAdded()
    {
        var state = new NodeEditorState();
        using var bus = new PluginEventBus(state);

        var received = false;
        using var subscription = bus.SubscribeNodeAdded(args =>
        {
            received = args.Node.Data.Id == "node-1";
        });

        var node = new NodeViewModel(new NodeData(
            "node-1",
            "Test",
            false,
            false,
            Array.Empty<SocketData>(),
            Array.Empty<SocketData>()));

        state.AddNode(node);

        Assert.True(received);
    }

    [Fact]
    public void PluginEventBus_DisposeUnhooksState()
    {
        var state = new NodeEditorState();
        var bus = new PluginEventBus(state);

        var calls = 0;
        var subscription = bus.SubscribeNodeAdded(_ => calls++);

        bus.Dispose();
        subscription.Dispose();

        var node = new NodeViewModel(new NodeData(
            "node-2",
            "Test",
            false,
            false,
            Array.Empty<SocketData>(),
            Array.Empty<SocketData>()));

        state.AddNode(node);

        Assert.Equal(0, calls);
    }
}