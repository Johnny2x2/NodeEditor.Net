using System.Text.Json;
using System.Text.Json.Serialization;

namespace NodeEditor.Net.Models;

/// <summary>
/// JSON converter for <see cref="SerializableDict"/> that serializes as a JSON object
/// with string keys and heterogeneous values.
/// </summary>
public sealed class SerializableDictJsonConverter : JsonConverter<SerializableDict>
{
    public override SerializableDict Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dict = new SerializableDict();

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"Expected StartObject but got {reader.TokenType}.");

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected property name.");

            var key = reader.GetString()!;
            reader.Read();

            var element = JsonElement.ParseValue(ref reader);
            dict.Set(key, DeserializeElement(element));
        }

        return dict;
    }

    public override void Write(Utf8JsonWriter writer, SerializableDict value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var kvp in value.Snapshot())
        {
            writer.WritePropertyName(kvp.Key);
            JsonSerializer.Serialize(writer, kvp.Value, kvp.Value?.GetType() ?? typeof(object), options);
        }

        writer.WriteEndObject();
    }

    private static object DeserializeElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString()!,
        JsonValueKind.Number when element.TryGetInt32(out var i) => i,
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null!,
        _ => element // Keep as JsonElement for nested objects/arrays
    };
}
