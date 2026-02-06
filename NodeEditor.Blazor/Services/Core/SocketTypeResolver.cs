namespace NodeEditor.Blazor.Services;

public sealed class SocketTypeResolver : ISocketTypeResolver
{
    private readonly Dictionary<string, Type> _registry = new(StringComparer.Ordinal);

    public void Register<T>() => Register(typeof(T));

    public void Register(Type type)
    {
        var key = type.FullName ?? type.Name;
        _registry[key] = type;
    }

    public Type? Resolve(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return null;
        }

        if (_registry.TryGetValue(typeName, out var registered))
        {
            return registered;
        }

        return Type.GetType(typeName, throwOnError: false);
    }
}
