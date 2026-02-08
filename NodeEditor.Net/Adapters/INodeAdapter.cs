using NodeEditor.Net.Models;

namespace NodeEditor.Net.Adapters;

/// <summary>
/// Converts legacy node snapshot formats to the current <see cref="NodeData"/> model.
/// Injectable and mockable replacement for the previously static adapter.
/// </summary>
public interface INodeAdapter
{
    /// <summary>
    /// Converts a <see cref="LegacyNodeSnapshot"/> to a <see cref="NodeData"/> record.
    /// </summary>
    NodeData FromSnapshot(LegacyNodeSnapshot snapshot);
}
