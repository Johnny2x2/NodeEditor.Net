namespace NodeEditor.Blazor.Services;

public interface ISocketTypeResolver
{
    void Register<T>();
    void Register(Type type);
    Type? Resolve(string? typeName);
}
