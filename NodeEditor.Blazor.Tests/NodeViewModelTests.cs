using NodeEditor.Net.Models;
using NodeEditor.Net.ViewModels;

namespace NodeEditor.Blazor.Tests;

public sealed class NodeViewModelTests
{
    [Fact]
    public void NodeViewModel_BuildsSocketViewModels()
    {
        var data = new NodeData(
            "node-1",
            "Test",
            false,
            false,
            new[] { new SocketData("In", "System.Int32", true, false) },
            new[] { new SocketData("Out", "System.Int32", false, false) });

        var viewModel = new NodeViewModel(data);

        Assert.Single(viewModel.Inputs);
        Assert.Single(viewModel.Outputs);
        Assert.Equal("In", viewModel.Inputs[0].Data.Name);
        Assert.Equal("Out", viewModel.Outputs[0].Data.Name);
    }
}
