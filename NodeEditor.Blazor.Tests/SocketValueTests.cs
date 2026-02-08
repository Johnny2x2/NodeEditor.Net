using System.Text.Json;
using NodeEditor.Net.Models;

namespace NodeEditor.Blazor.Tests;

public sealed class SocketValueTests
{
    [Fact]
    public void FromObject_SerializesAndDeserializes()
    {
        var original = 42;
        var socketValue = SocketValue.FromObject(original);

        Assert.Equal(typeof(int).FullName, socketValue.TypeName);
        Assert.True(socketValue.Json.HasValue);

        var deserialized = socketValue.ToObject<int>();
        Assert.Equal(original, deserialized);
    }

    [Fact]
    public void FromObject_NullProducesEmptyPayload()
    {
        var socketValue = SocketValue.FromObject(null);

        Assert.Null(socketValue.TypeName);
        Assert.False(socketValue.Json.HasValue);
    }

    [Fact]
    public void ToObject_ReturnsDefaultWhenJsonMissing()
    {
        var socketValue = new SocketValue("System.Int32", null);

        var result = socketValue.ToObject<int>();

        Assert.Equal(default, result);
    }
}
