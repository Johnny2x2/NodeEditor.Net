using System.Text.Json;
using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Execution;

namespace NodeEditor.Net.Services;

/// <summary>
/// Handles execution of Get Variable and Set Variable nodes during graph execution.
/// Variable nodes don't have backing [Node]-attributed methods; instead they read/write
/// the execution context's variable store, which is seeded from GraphVariable default values.
/// </summary>
public static class VariableNodeExecutor
{
    /// <summary>
    /// Returns true if the given node is a variable node (Get or Set).
    /// </summary>
    public static bool IsVariableNode(NodeData node)
    {
        if (string.IsNullOrEmpty(node.DefinitionId))
            return false;

        return node.DefinitionId.StartsWith(GraphVariable.GetDefinitionPrefix, StringComparison.Ordinal)
               || node.DefinitionId.StartsWith(GraphVariable.SetDefinitionPrefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// Extracts the variable ID from a variable node's definition ID.
    /// </summary>
    public static string? GetVariableId(NodeData node)
    {
        if (string.IsNullOrEmpty(node.DefinitionId))
            return null;

        if (node.DefinitionId.StartsWith(GraphVariable.GetDefinitionPrefix, StringComparison.Ordinal))
            return node.DefinitionId[GraphVariable.GetDefinitionPrefix.Length..];

        if (node.DefinitionId.StartsWith(GraphVariable.SetDefinitionPrefix, StringComparison.Ordinal))
            return node.DefinitionId[GraphVariable.SetDefinitionPrefix.Length..];

        return null;
    }

    /// <summary>
    /// Returns true if the node is a Get Variable node.
    /// </summary>
    public static bool IsGetNode(NodeData node)
    {
        return node.DefinitionId?.StartsWith(GraphVariable.GetDefinitionPrefix, StringComparison.Ordinal) == true;
    }

    /// <summary>
    /// Returns true if the node is a Set Variable node.
    /// </summary>
    public static bool IsSetNode(NodeData node)
    {
        return node.DefinitionId?.StartsWith(GraphVariable.SetDefinitionPrefix, StringComparison.Ordinal) == true;
    }

    /// <summary>
    /// Executes a variable node: reads from or writes to the execution context's variable store.
    /// </summary>
    public static void Execute(NodeData node, INodeRuntimeStorage context)
    {
        var variableId = GetVariableId(node)
            ?? throw new InvalidOperationException($"Cannot extract variable ID from node '{node.Name}'.");

        if (IsGetNode(node))
        {
            // Read variable value from context and write to the node's output socket
            var value = context.GetVariable(variableId);
            context.SetSocketValue(node.Id, "Value", value);
        }
        else if (IsSetNode(node))
        {
            // Read input value from node's input socket and store in context variable
            var value = context.GetSocketValue(node.Id, "Value");
            context.SetVariable(variableId, value);

            // Pass-through: also write to output "Value" socket so downstream data-flow works
            context.SetSocketValue(node.Id, "Value", value);

            // Signal execution path
            var exit = new ExecutionPath();
            exit.Signal();
            context.SetSocketValue(node.Id, "Exit", exit);
        }

        context.MarkNodeExecuted(node.Id);
    }

    /// <summary>
    /// Seeds the execution context with default values from all graph variables.
    /// Should be called before execution starts.
    /// </summary>
    public static void SeedVariables(INodeRuntimeStorage context, IEnumerable<GraphVariable> variables, ISocketTypeResolver? typeResolver = null)
    {
        foreach (var variable in variables)
        {
            if (variable.DefaultValue?.Json is not null)
            {
                // Try to resolve the concrete type so we get a proper object, not a raw JsonElement
                Type? targetType = null;
                if (typeResolver is not null)
                {
                    targetType = typeResolver.Resolve(variable.TypeName);
                }

                targetType ??= Type.GetType(variable.TypeName, throwOnError: false);

                object? value;
                if (targetType is not null)
                {
                    value = variable.DefaultValue.Json.Value.Deserialize(targetType);
                }
                else
                {
                    value = variable.DefaultValue.Json.Value.Deserialize<object>();
                }

                context.SetVariable(variable.Id, value);
            }
            else
            {
                context.SetVariable(variable.Id, null);
            }
        }
    }
}
