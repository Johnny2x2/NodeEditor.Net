using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Execution;

namespace NodeEditor.Net.Services.Registry;

public sealed record class NodeDefinition(
    string Id,
    string Name,
    string Category,
    string Description,
    IReadOnlyList<SocketData> Inputs,
    IReadOnlyList<SocketData> Outputs,
    Func<NodeData> Factory,
    Type? NodeType = null,
    Func<INodeExecutionContext, CancellationToken, Task>? InlineExecutor = null,
    IReadOnlyList<StreamSocketInfo>? StreamSockets = null);
