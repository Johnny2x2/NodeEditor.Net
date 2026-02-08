using System.Reflection;
using System.Text.Json;
using NodeEditor.Net.Models;
using NodeEditor.Net.Services;

namespace NodeEditor.Net.Services.Execution;

public sealed class NodeMethodInvoker
{
    private readonly object _context;
    private readonly ISocketTypeResolver _typeResolver;
    private readonly Dictionary<string, NodeMethodBinding> _methodMap;
    private readonly Dictionary<string, NodeMethodBinding> _definitionIdMap;

    public NodeMethodInvoker(object context, ISocketTypeResolver typeResolver)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _typeResolver = typeResolver ?? throw new ArgumentNullException(nameof(typeResolver));
        _methodMap = context is INodeContextHost host
            ? BuildMethodMap(host.Contexts, out _definitionIdMap)
            : BuildMethodMap(new[] { context }, out _definitionIdMap);
    }

    /// <summary>
    /// Resolves a node to its method binding.
    /// Prioritizes DefinitionId lookup over Name lookup for disambiguation.
    /// </summary>
    public NodeMethodBinding? Resolve(NodeData node)
    {
        // Try DefinitionId first for unambiguous resolution
        if (!string.IsNullOrEmpty(node.DefinitionId) && 
            _definitionIdMap.TryGetValue(node.DefinitionId, out var definitionMethod))
        {
            return definitionMethod;
        }
        
        // Fall back to Name-based lookup
        if (_methodMap.TryGetValue(node.Name, out var method))
        {
            return method;
        }

        return null;
    }

    public async Task InvokeAsync(NodeData node, NodeMethodBinding binding, INodeExecutionContext executionContext, CancellationToken token)
    {
        if (binding is null)
        {
            throw new ArgumentNullException(nameof(binding));
        }

        var parameters = binding.Method.GetParameters();
        var args = new object?[parameters.Length];
        var outParameters = new List<(int Index, string SocketName, Type SocketType)>();

        for (var i = 0; i < parameters.Length; i++)
        {
            token.ThrowIfCancellationRequested();

            var parameter = parameters[i];
            var parameterType = parameter.ParameterType;
            var isByRef = parameterType.IsByRef;
            var isOut = parameter.IsOut;
            var socketName = parameter.Name ?? string.Empty;

            if (parameterType == typeof(CancellationToken))
            {
                args[i] = token;
                continue;
            }

            if (isByRef)
            {
                parameterType = parameterType.GetElementType() ?? parameterType;
            }

            if (isOut || isByRef)
            {
                args[i] = CreateDefault(parameterType);
                outParameters.Add((i, socketName, parameterType));
                continue;
            }

            var value = GetInputValue(node, socketName, parameterType, executionContext);
            args[i] = value;
        }

        var result = binding.Method.Invoke(binding.Target, args);

        if (result is Task task)
        {
            await task.ConfigureAwait(false);
        }

        foreach (var (index, socketName, socketType) in outParameters)
        {
            var value = args[index];
            if (value is null && socketType == typeof(ExecutionPath))
            {
                value = new ExecutionPath();
            }

            if (!string.IsNullOrWhiteSpace(socketName))
            {
                executionContext.SetSocketValue(node.Id, socketName, value);
            }
        }

        executionContext.MarkNodeExecuted(node.Id);
    }

    private object? GetInputValue(NodeData node, string socketName, Type targetType, INodeExecutionContext executionContext)
    {
        if (!string.IsNullOrWhiteSpace(socketName) && executionContext.TryGetSocketValue(node.Id, socketName, out var stored))
        {
            return ConvertValue(stored, targetType);
        }

        var socket = node.Inputs.FirstOrDefault(s => s.Name.Equals(socketName, StringComparison.Ordinal));
        if (socket?.Value is not null)
        {
            return ConvertSocketValue(socket.Value, targetType);
        }

        if (targetType == typeof(ExecutionPath))
        {
            return new ExecutionPath();
        }

        if (targetType.IsValueType)
        {
            return Activator.CreateInstance(targetType);
        }

        return null;
    }

    private object? ConvertSocketValue(SocketValue socketValue, Type targetType)
    {
        if (socketValue.Json is null)
        {
            return null;
        }

        if (targetType == typeof(object))
        {
            var resolved = _typeResolver.Resolve(socketValue.TypeName);
            if (resolved is not null)
            {
                targetType = resolved;
            }
        }

        return socketValue.Json.Value.Deserialize(targetType);
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value is null)
        {
            return null;
        }

        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }

        try
        {
            return Convert.ChangeType(value, targetType);
        }
        catch
        {
            return value;
        }
    }

    private static object? CreateDefault(Type type)
    {
        if (type.IsValueType)
        {
            return Activator.CreateInstance(type);
        }

        return null;
    }

    private static Dictionary<string, NodeMethodBinding> BuildMethodMap(
        IEnumerable<object> contexts,
        out Dictionary<string, NodeMethodBinding> definitionIdMap)
    {
        var map = new Dictionary<string, NodeMethodBinding>(StringComparer.Ordinal);
        definitionIdMap = new Dictionary<string, NodeMethodBinding>(StringComparer.Ordinal);

        foreach (var context in contexts)
        {
            var contextType = context.GetType();
            foreach (var method in contextType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                // Build DefinitionId for this method
                var definitionId = BuildDefinitionId(contextType, method);
                
                // Try to get NodeAttribute - handle cross-context loading by checking both
                // direct attribute and name-based matching for dynamically loaded plugins
                var attribute = method.GetCustomAttribute<NodeAttribute>();
                if (attribute is not null)
                {
                    var binding = new NodeMethodBinding(context, method, definitionId);
                    map.TryAdd(attribute.Name, binding);
                    definitionIdMap.TryAdd(definitionId, binding);
                    continue;
                }

                // For cross-context compatibility, also check by attribute type name
                var nodeAttr = method.GetCustomAttributes()
                    .FirstOrDefault(a => a.GetType().Name == nameof(NodeAttribute));
                if (nodeAttr is not null)
                {
                    // Get the Name property via reflection
                    var nameProp = nodeAttr.GetType().GetProperty("Name");
                    if (nameProp?.GetValue(nodeAttr) is string nodeName && !string.IsNullOrEmpty(nodeName))
                    {
                        var binding = new NodeMethodBinding(context, method, definitionId);
                        map.TryAdd(nodeName, binding);
                        definitionIdMap.TryAdd(definitionId, binding);
                        continue;
                    }
                }

                // Fallback: use method name
                var fallbackBinding = new NodeMethodBinding(context, method, definitionId);
                map.TryAdd(method.Name, fallbackBinding);
                definitionIdMap.TryAdd(definitionId, fallbackBinding);
            }
        }

        return map;
    }
    
    private static string BuildDefinitionId(Type contextType, MethodInfo method)
    {
        try
        {
            // Force eager evaluation inside try-catch to handle missing dependencies
            var parameters = method.GetParameters()
                .Select(p => p.ParameterType.IsByRef
                    ? p.ParameterType.GetElementType() ?? p.ParameterType
                    : p.ParameterType)
                .Select(t => t.FullName ?? t.Name)
                .ToList(); // Force eager evaluation

            var signature = string.Join(",", parameters);
            return $"{contextType.FullName}.{method.Name}({signature})";
        }
        catch (FileNotFoundException)
        {
            // If we can't load parameter types (missing dependencies), use a simpler signature
            return $"{contextType.FullName}.{method.Name}";
        }
        catch (TypeLoadException)
        {
            // If we can't load parameter types (missing dependencies), use a simpler signature
            return $"{contextType.FullName}.{method.Name}";
        }
    }
}

/// <summary>
/// Represents a binding between a node and its implementation method.
/// </summary>
/// <param name="Target">The context instance containing the method.</param>
/// <param name="Method">The method to invoke.</param>
/// <param name="DefinitionId">Optional unique identifier for the node definition.</param>
public sealed record NodeMethodBinding(object Target, MethodInfo Method, string? DefinitionId);
