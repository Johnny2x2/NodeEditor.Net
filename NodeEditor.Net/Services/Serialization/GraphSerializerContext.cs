using System.Text.Json.Serialization;
using NodeEditor.Net.Models;

namespace NodeEditor.Net.Services.Serialization;

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
[JsonSerializable(typeof(GraphVariableDto))]
[JsonSerializable(typeof(PluginDependencyDto))]
[JsonSerializable(typeof(GraphEventDto))]
[JsonSerializable(typeof(OverlayDto))]
public partial class GraphSerializerContext : JsonSerializerContext
{
}
