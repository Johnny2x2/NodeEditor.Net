using NodeEditor.Blazor.Models;
using System.Collections.ObjectModel;
using System.Linq;

namespace NodeEditor.Blazor.ViewModels;

public sealed class NodeViewModel : ViewModelBase
{
    private Point2D _position;
    private Size2D _size;
    private bool _isSelected;

    public NodeViewModel(NodeData data)
    {
        Data = data;
        _size = new Size2D(140, 60);
        Inputs = new ReadOnlyCollection<SocketViewModel>((data.Inputs ?? Array.Empty<SocketData>())
            .Select(socket => new SocketViewModel(socket))
            .ToList());
        Outputs = new ReadOnlyCollection<SocketViewModel>((data.Outputs ?? Array.Empty<SocketData>())
            .Select(socket => new SocketViewModel(socket))
            .ToList());
    }

    public NodeData Data { get; }

    public IReadOnlyList<SocketViewModel> Inputs { get; }

    public IReadOnlyList<SocketViewModel> Outputs { get; }

    public Point2D Position
    {
        get => _position;
        set => SetProperty(ref _position, value);
    }

    public Size2D Size
    {
        get => _size;
        set => SetProperty(ref _size, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
