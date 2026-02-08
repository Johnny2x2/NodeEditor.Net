using System.Text.Json;
using System.Text.Json.Serialization;

namespace NodeEditor.Blazor.Models;

/// <summary>
/// JSON converter for <see cref="SerializableList"/> that serializes the list as a JSON array
/// and deserializes a JSON array back into a <see cref="SerializableList"/>.
/// Without this converter, <see cref="SerializableList"/> (which has only private fields)
/// would serialize as <c>{}</c> and lose all items on roundtrip.
/// </summary>
public sealed class SerializableListJsonConverter : JsonConverter<SerializableList>
{
    public override SerializableList Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var list = new SerializableList();

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            // Handle legacy format (empty object "{}") — skip to end
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                // skip any properties in the object
                reader.Skip();
            }

            return list;
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"Expected StartArray or StartObject but got {reader.TokenType}.");
        }

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                break;

            var element = JsonElement.ParseValue(ref reader);
            var value = DeserializeElement(element);
            list.Add(value);
        }

        return list;
    }

    public override void Write(Utf8JsonWriter writer, SerializableList value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (var item in value.Snapshot())
        {
            JsonSerializer.Serialize(writer, item, item?.GetType() ?? typeof(object), options);
        }

        writer.WriteEndArray();
    }

    private static object DeserializeElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString()!,
        JsonValueKind.Number when element.TryGetInt32(out var i) => i,
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null!,
        JsonValueKind.Array => DeserializeArray(element),
        _ => element // Keep as JsonElement for objects/unknown
    };

    private static SerializableList DeserializeArray(JsonElement array)
    {
        var inner = new SerializableList();
        foreach (var item in array.EnumerateArray())
        {
            inner.Add(DeserializeElement(item));
        }

        return inner;
    }
}
