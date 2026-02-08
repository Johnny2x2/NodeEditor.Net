using System.Text.Json;

namespace NodeEditor.Net.Models;

public sealed record class SocketValue(string? TypeName, JsonElement? Json)
{
    public static SocketValue FromObject(object? value)
    {
        if (value is null)
        {
            return new SocketValue(null, null);
        }

        var type = value.GetType();
        var json = JsonSerializer.SerializeToElement(value, type);
        return new SocketValue(type.FullName ?? type.Name, json);
    }

    public T? ToObject<T>()
    {
        if (Json is null)
        {
            return default;
        }

        return Json.Value.Deserialize<T>();
    }
}
