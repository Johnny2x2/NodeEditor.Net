using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution;

public sealed class NodeBuilder : INodeBuilder
{
    private string _name = "Node";
    private string _category = "General";
    private string _description = string.Empty;
    private bool _callable;
    private bool _execInit;
    private readonly List<SocketData> _inputs = new();
    private readonly List<SocketData> _outputs = new();
    private Func<INodeExecutionContext, CancellationToken, Task>? _inlineExecutor;
    private readonly List<StreamSocketInfo> _streamSockets = new();
    internal Type? NodeType { get; set; }

    private NodeBuilder() { }

    /// <summary>Static entry for standalone (non-subclass) node creation.</summary>
    public static NodeBuilder Create(string name) => new() { _name = name };

    /// <summary>Creates a builder for a NodeBase subclass during discovery.</summary>
    internal static NodeBuilder CreateForType(Type nodeType) => new() { NodeType = nodeType };

    // ── Metadata ──
    public INodeBuilder Name(string name) { _name = name; return this; }
    public INodeBuilder Category(string category) { _category = category; return this; }
    public INodeBuilder Description(string description) { _description = description; return this; }

    // ── Execution sockets ──
    public INodeBuilder Callable()
    {
        _callable = true;
        AddSocketIfMissing(_inputs, new SocketData("Enter", ExecutionSocketTypeName, true, true));
        AddSocketIfMissing(_outputs, new SocketData("Exit", ExecutionSocketTypeName, false, true));
        return this;
    }

    public INodeBuilder ExecutionInitiator()
    {
        _callable = true;
        _execInit = true;
        AddSocketIfMissing(_outputs, new SocketData("Exit", ExecutionSocketTypeName, false, true));
        return this;
    }

    public INodeBuilder ExecutionInput(string name)
    {
        _callable = true;
        AddSocketIfMissing(_inputs, new SocketData(name, ExecutionSocketTypeName, true, true));
        return this;
    }

    public INodeBuilder ExecutionOutput(string name)
    {
        AddSocketIfMissing(_outputs, new SocketData(name, ExecutionSocketTypeName, false, true));
        return this;
    }

    // ── Data sockets ──
    public INodeBuilder Input<T>(string name, T? defaultValue = default, SocketEditorHint? editorHint = null)
    {
        var socketValue = defaultValue is not null ? SocketValue.FromObject(defaultValue) : null;
        AddSocketIfMissing(_inputs, new SocketData(name, typeof(T).FullName!, true, false, socketValue, editorHint));
        return this;
    }

    public INodeBuilder Input(string name, string typeName, SocketValue? defaultValue = null, SocketEditorHint? editorHint = null)
    {
        AddSocketIfMissing(_inputs, new SocketData(name, typeName, true, false, defaultValue, editorHint));
        return this;
    }

    public INodeBuilder Output<T>(string name)
    {
        AddSocketIfMissing(_outputs, new SocketData(name, typeof(T).FullName!, false, false));
        return this;
    }

    public INodeBuilder Output(string name, string typeName)
    {
        AddSocketIfMissing(_outputs, new SocketData(name, typeName, false, false));
        return this;
    }

    // ── Streaming ──
    public INodeBuilder StreamOutput<T>(string itemSocketName, string onItemExecName = "OnItem",
        string? completedExecName = "Completed")
        => StreamOutput(itemSocketName, typeof(T).FullName!, onItemExecName, completedExecName);

    public INodeBuilder StreamOutput(string itemSocketName, string typeName,
        string onItemExecName = "OnItem", string? completedExecName = "Completed")
    {
        AddSocketIfMissing(_outputs, new SocketData(itemSocketName, typeName, false, false));
        AddSocketIfMissing(_outputs, new SocketData(onItemExecName, ExecutionSocketTypeName, false, true));
        if (completedExecName is not null)
            AddSocketIfMissing(_outputs, new SocketData(completedExecName, ExecutionSocketTypeName, false, true));
        _streamSockets.Add(new StreamSocketInfo(itemSocketName, onItemExecName, completedExecName));
        return this;
    }

    // ── Inline execution ──
    public INodeBuilder OnExecute(Func<INodeExecutionContext, CancellationToken, Task> executor)
    {
        _inlineExecutor = executor;
        return this;
    }

    // ── Build ──
    public NodeDefinition Build()
    {
        var id = BuildDefinitionId();
        var inputsSnapshot = _inputs.ToArray().AsReadOnly();
        var outputsSnapshot = _outputs.ToArray().AsReadOnly();
        var name = _name;
        var callable = _callable;
        var execInit = _execInit;

        return new NodeDefinition(
            Id: id,
            Name: name,
            Category: _category,
            Description: _description,
            Inputs: inputsSnapshot,
            Outputs: outputsSnapshot,
            Factory: () => new NodeData(
                Id: Guid.NewGuid().ToString("N"),
                Name: name,
                Callable: callable,
                ExecInit: execInit,
                Inputs: inputsSnapshot,
                Outputs: outputsSnapshot,
                DefinitionId: id),
            NodeType: NodeType,
            InlineExecutor: _inlineExecutor,
            StreamSockets: _streamSockets.Count > 0 ? _streamSockets.AsReadOnly() : null);
    }

    private string BuildDefinitionId()
    {
        if (NodeType is not null) return NodeType.FullName ?? NodeType.Name;
        return _name;
    }

    private static void AddSocketIfMissing(List<SocketData> list, SocketData socket)
    {
        if (!list.Any(s => s.Name == socket.Name && s.IsInput == socket.IsInput))
            list.Add(socket);
    }

    private static readonly string ExecutionSocketTypeName = ExecutionSocket.TypeName;
}
