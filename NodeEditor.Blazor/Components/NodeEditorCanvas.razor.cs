using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.ViewModels;

namespace NodeEditor.Blazor.Components;

/// <summary>
/// Event arguments for socket pointer interactions.
/// </summary>
public sealed class SocketPointerEventArgs : EventArgs
{
    public required string NodeId { get; init; }
    public required SocketViewModel Socket { get; init; }
    public required Point2D Position { get; init; }
}

/// <summary>
/// Event arguments for node pointer interactions.
/// </summary>
public sealed class NodePointerEventArgs : EventArgs
{
    public required string NodeId { get; init; }
    public required Point2D Position { get; init; }
}
