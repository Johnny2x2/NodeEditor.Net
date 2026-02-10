using System;
using System.Threading;
using System.Threading.Tasks;
using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution;

/// <summary>
/// Fluent API for defining a node's metadata and sockets.
/// Used inside NodeBase.Configure() and as a standalone factory.
/// </summary>
public interface INodeBuilder
{
    // ── Metadata ──
    INodeBuilder Name(string name);
    INodeBuilder Category(string category);
    INodeBuilder Description(string description);

    // ── Execution sockets ──
    INodeBuilder Callable();
    INodeBuilder ExecutionInitiator();
    INodeBuilder ExecutionInput(string name);
    INodeBuilder ExecutionOutput(string name);

    // ── Data sockets ──
    INodeBuilder Input<T>(string name, T? defaultValue = default, SocketEditorHint? editorHint = null);
    INodeBuilder Input(string name, string typeName, SocketValue? defaultValue = null, SocketEditorHint? editorHint = null);
    INodeBuilder Output<T>(string name);
    INodeBuilder Output(string name, string typeName);

    // ── Streaming ──
    INodeBuilder StreamOutput<T>(string itemSocketName, string onItemExecName = "OnItem",
        string? completedExecName = "Completed");
    INodeBuilder StreamOutput(string itemSocketName, string typeName,
        string onItemExecName = "OnItem", string? completedExecName = "Completed");

    // ── Inline execution ──
    INodeBuilder OnExecute(Func<INodeExecutionContext, CancellationToken, Task> executor);
}
