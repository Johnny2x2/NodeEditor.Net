using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Services;

public interface IConnectionValidator
{
    bool CanConnect(SocketData output, SocketData input);
}
