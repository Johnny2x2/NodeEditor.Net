using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.ViewModels;

public sealed class SocketViewModel : ViewModelBase
{
    private Point2D _position;
    private Size2D _size = new(12, 12);

    public SocketViewModel(SocketData data)
    {
        Data = data;
    }

    public SocketData Data { get; }

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
}
