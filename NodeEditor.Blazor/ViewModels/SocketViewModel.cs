using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.ViewModels;

public sealed class SocketViewModel : ViewModelBase
{
    private Point2D _position;
    private Size2D _size = new(12, 12);
    private SocketData _data;

    public SocketViewModel(SocketData data)
    {
        _data = data;
    }

    public SocketData Data
    {
        get => _data;
        private set => SetProperty(ref _data, value);
    }

    public void SetValue(object? value)
    {
        Data = Data with { Value = SocketValue.FromObject(value) };
    }

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
