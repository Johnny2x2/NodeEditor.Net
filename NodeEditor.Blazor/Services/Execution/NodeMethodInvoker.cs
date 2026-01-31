using System.Reflection;
using System.Text.Json;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services;

namespace NodeEditor.Blazor.Services.Execution;

public sealed class NodeMethodInvoker
{
    private readonly object _context;
    private readonly SocketTypeResolver _typeResolver;
    private readonly Dictionary<string, MethodInfo> _methodMap;

    public NodeMethodInvoker(object context, SocketTypeResolver typeResolver)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _typeResolver = typeResolver ?? throw new ArgumentNullException(nameof(typeResolver));
        _methodMap = BuildMethodMap(context.GetType());
    }

    public MethodInfo? Resolve(NodeData node)
    {
        if (_methodMap.TryGetValue(node.Name, out var method))
        {
            return method;
        }

        return null;
    }

    public async Task InvokeAsync(NodeData node, MethodInfo method, INodeExecutionContext executionContext, CancellationToken token)
    {
        if (method is null)
        {
            throw new ArgumentNullException(nameof(method));
        }

        var parameters = method.GetParameters();
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

        var result = method.Invoke(_context, args);

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

    private static Dictionary<string, MethodInfo> BuildMethodMap(Type contextType)
    {
        var map = new Dictionary<string, MethodInfo>(StringComparer.Ordinal);
        foreach (var method in contextType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var attribute = method.GetCustomAttribute<NodeAttribute>();
            if (attribute is not null)
            {
                map[attribute.Name] = method;
                continue;
            }

            map.TryAdd(method.Name, method);
        }

        return map;
    }
}
