using System.Linq;
using NodeEditor.Net.Models;

namespace NodeEditor.Net.Adapters;

/// <summary>
/// Converts legacy node snapshot formats to the current <see cref="NodeData"/> model.
/// </summary>
public sealed class NodeAdapter : INodeAdapter
{
    /// <inheritdoc />
    public NodeData FromSnapshot(LegacyNodeSnapshot snapshot)
    {
        var inputs = snapshot.Inputs
            .Select(socket => new SocketData(
                socket.Name,
                socket.TypeName,
                IsInput: true,
                IsExecution: socket.IsExecution,
                Value: socket.Value))
            .ToList();

        var outputs = snapshot.Outputs
            .Select(socket => new SocketData(
                socket.Name,
                socket.TypeName,
                IsInput: false,
                IsExecution: socket.IsExecution,
                Value: socket.Value))
            .ToList();

        return new NodeData(
            snapshot.Id,
            snapshot.Name,
            snapshot.Callable,
            snapshot.ExecInit,
            inputs,
            outputs);
    }
}
