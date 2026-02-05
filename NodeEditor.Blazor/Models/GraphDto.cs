namespace NodeEditor.Blazor.Models;

public sealed record class GraphDto(
    int Version,
    List<NodeDto> Nodes,
    List<ConnectionDto> Connections,
    ViewportDto Viewport,
    List<string> SelectedNodeIds,
    List<GraphVariableDto>? Variables = null);

public sealed record class NodeDto(
    string Id,
    string? TypeId,
    string Name,
    bool Callable,
    bool ExecInit,
    double X,
    double Y,
    double Width,
    double Height,
    List<SocketData> Inputs,
    List<SocketData> Outputs);

public sealed record class ConnectionDto(
    string OutputNodeId,
    string OutputSocketName,
    string InputNodeId,
    string InputSocketName,
    bool IsExecution);

public sealed record class ViewportDto(
    double X,
    double Y,
    double Width,
    double Height,
    double Zoom);

public sealed record class GraphVariableDto(
    string Id,
    string Name,
    string TypeName,
    SocketValue? DefaultValue = null);
