using NodeEditor.Net.Services;

namespace NodeEditor.Blazor.Tests;

public sealed class SocketTypeResolverTests
{
    [Fact]
    public void Resolve_ReturnsRegisteredType()
    {
        var resolver = new SocketTypeResolver();
        resolver.Register<int>();

        var result = resolver.Resolve(typeof(int).FullName);

        Assert.Equal(typeof(int), result);
    }

    [Fact]
    public void Resolve_ReturnsNullForUnknownType()
    {
        var resolver = new SocketTypeResolver();

        var result = resolver.Resolve("Missing.Type");

        Assert.Null(result);
    }
}
