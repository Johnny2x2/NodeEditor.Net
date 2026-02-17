using NodeEditor.Net.Models;

namespace NodeEditor.Net.Services.Infrastructure;

public interface IConnectionValidator
{
    bool CanConnect(SocketData output, SocketData input);
}
