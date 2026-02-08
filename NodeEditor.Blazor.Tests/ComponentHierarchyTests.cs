using NodeEditor.Net.Models;
using NodeEditor.Net.Services;
using NodeEditor.Net.Services.Infrastructure;
using NodeEditor.Net.ViewModels;

namespace NodeEditor.Blazor.Tests;

/// <summary>
/// Stage 3 smoke tests for Blazor component hierarchy.
/// These tests verify that the component data structures render correctly.
/// </summary>
public sealed class ComponentHierarchyTests
{
    [Fact]
    public void NodeEditorState_RendersMultipleNodes_CanAddFiveNodes()
    {
        // Arrange
        var state = new NodeEditorState();
        var nodes = CreateTestNodes(5);

        // Act
        foreach (var node in nodes)
        {
            state.AddNode(node);
        }

        // Assert
        Assert.Equal(5, state.Nodes.Count);
        Assert.Equal(5, state.Nodes.Select(n => n.Data.Name).Distinct().Count());
    }

    [Fact]
    public void NodeEditorState_RendersConnections_CanAddSixConnections()
    {
        // Arrange
        var state = new NodeEditorState();
        var nodes = CreateTestNodes(5);
        foreach (var node in nodes)
        {
            state.AddNode(node);
        }

        // Act - Create 6 connections between nodes
        var connections = new List<ConnectionData>
        {
            new("node-0", "node-1", "Output1", "Input1", false),
            new("node-1", "node-2", "Output1", "Input1", false),
            new("node-2", "node-3", "Output1", "Input1", false),
            new("node-3", "node-4", "Output1", "Input1", false),
            new("node-0", "node-2", "Output2", "Input2", true), // Execution
            new("node-1", "node-3", "Output2", "Input2", true), // Execution
        };

        foreach (var conn in connections)
        {
            state.AddConnection(conn);
        }

        // Assert
        Assert.Equal(6, state.Connections.Count);
        Assert.Equal(2, state.Connections.Count(c => c.IsExecution));
        Assert.Equal(4, state.Connections.Count(c => !c.IsExecution));
    }

    [Fact]
    public void NodeViewModel_HasCorrectSocketCounts()
    {
        // Arrange
        var nodeData = new NodeData(
            Id: "test-node",
            Name: "Test Node",
            Callable: true,
            ExecInit: false,
            Inputs: new List<SocketData>
            {
                new("Input1", "int", true, false),
                new("Input2", "string", true, false),
                new("ExecIn", "execution", true, true)
            },
            Outputs: new List<SocketData>
            {
                new("Output1", "int", false, false),
                new("ExecOut", "execution", false, true)
            });

        // Act
        var vm = new NodeViewModel(nodeData);

        // Assert
        Assert.Equal(3, vm.Inputs.Count);
        Assert.Equal(2, vm.Outputs.Count);
        Assert.Equal(1, vm.Inputs.Count(s => s.Data.IsExecution));
        Assert.Equal(1, vm.Outputs.Count(s => s.Data.IsExecution));
    }

    [Fact]
    public void NodeViewModel_PositionAndSize_AreSettable()
    {
        // Arrange
        var nodeData = CreateNodeData("pos-test", "Position Test");
        var vm = new NodeViewModel(nodeData);

        // Act
        vm.Position = new Point2D(100, 200);
        vm.Size = new Size2D(180, 120);

        // Assert
        Assert.Equal(100, vm.Position.X);
        Assert.Equal(200, vm.Position.Y);
        Assert.Equal(180, vm.Size.Width);
        Assert.Equal(120, vm.Size.Height);
    }

    [Fact]
    public void CoordinateConverter_ScreenToGraph_ConvertsCorrectly()
    {
        // Arrange
        var converter = new CoordinateConverter
        {
            PanOffset = new Point2D(50, 100),
            Zoom = 2.0
        };

        // Act
        var graphPoint = converter.ScreenToGraph(new Point2D(150, 300));

        // Assert - (150 - 50) / 2 = 50, (300 - 100) / 2 = 100
        Assert.Equal(50, graphPoint.X);
        Assert.Equal(100, graphPoint.Y);
    }

    [Fact]
    public void CoordinateConverter_GraphToScreen_ConvertsCorrectly()
    {
        // Arrange
        var converter = new CoordinateConverter
        {
            PanOffset = new Point2D(50, 100),
            Zoom = 2.0
        };

        // Act
        var screenPoint = converter.GraphToScreen(new Point2D(50, 100));

        // Assert - 50 * 2 + 50 = 150, 100 * 2 + 100 = 300
        Assert.Equal(150, screenPoint.X);
        Assert.Equal(300, screenPoint.Y);
    }

    [Fact]
    public void CoordinateConverter_RoundTrip_PreservesPoint()
    {
        // Arrange
        var converter = new CoordinateConverter
        {
            PanOffset = new Point2D(75, 125),
            Zoom = 1.5
        };
        var original = new Point2D(200, 300);

        // Act
        var graphPoint = converter.ScreenToGraph(original);
        var roundTripped = converter.GraphToScreen(graphPoint);

        // Assert
        Assert.Equal(original.X, roundTripped.X, precision: 3);
        Assert.Equal(original.Y, roundTripped.Y, precision: 3);
    }

    [Fact]
    public void NodeEditorState_Selection_WorksCorrectly()
    {
        // Arrange
        var state = new NodeEditorState();
        var nodes = CreateTestNodes(3);
        foreach (var node in nodes)
        {
            state.AddNode(node);
        }

        // Act & Assert - Select first node
        state.SelectNode("node-0");
        Assert.Single(state.SelectedNodeIds);
        Assert.Contains("node-0", state.SelectedNodeIds);

        // Act & Assert - Toggle select second node (without clearing)
        state.ToggleSelectNode("node-1");
        Assert.Equal(2, state.SelectedNodeIds.Count);

        // Act & Assert - Clear selection
        state.ClearSelection();
        Assert.Empty(state.SelectedNodeIds);
    }

    [Fact]
    public void ConnectionData_StoresEndpointInfo()
    {
        // Arrange & Act
        var connection = new ConnectionData(
            OutputNodeId: "source-node",
            InputNodeId: "target-node",
            OutputSocketName: "Result",
            InputSocketName: "Value",
            IsExecution: false);

        // Assert
        Assert.Equal("source-node", connection.OutputNodeId);
        Assert.Equal("target-node", connection.InputNodeId);
        Assert.Equal("Result", connection.OutputSocketName);
        Assert.Equal("Value", connection.InputSocketName);
        Assert.False(connection.IsExecution);
    }

    #region Helper Methods

    private static List<NodeViewModel> CreateTestNodes(int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => new NodeViewModel(CreateNodeData($"node-{i}", $"Test Node {i}"))
            {
                Position = new Point2D(100 + i * 200, 100 + (i % 2) * 150)
            })
            .ToList();
    }

    private static NodeData CreateNodeData(string id, string name)
    {
        return new NodeData(
            Id: id,
            Name: name,
            Callable: id.GetHashCode() % 2 == 0,
            ExecInit: false,
            Inputs: new List<SocketData>
            {
                new("Input1", "int", true, false),
                new("Input2", "float", true, false)
            },
            Outputs: new List<SocketData>
            {
                new("Output1", "int", false, false),
                new("Output2", "string", false, false)
            });
    }

    #endregion
}
