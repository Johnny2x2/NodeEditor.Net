using System.Text.Json.Serialization;
using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Services.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(GraphDto))]
[JsonSerializable(typeof(NodeDto))]
[JsonSerializable(typeof(ConnectionDto))]
[JsonSerializable(typeof(ViewportDto))]
[JsonSerializable(typeof(SocketData))]
[JsonSerializable(typeof(SocketEditorHint))]
[JsonSerializable(typeof(SocketEditorKind))]
[JsonSerializable(typeof(SocketValue))]
public partial class GraphSerializerContext : JsonSerializerContext
{
}
