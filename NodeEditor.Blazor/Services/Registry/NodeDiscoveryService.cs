using System.Reflection;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services.Execution;

namespace NodeEditor.Blazor.Services.Registry;

public sealed class NodeDiscoveryService
{
    public IReadOnlyList<NodeDefinition> DiscoverFromAssemblies(IEnumerable<Assembly> assemblies)
    {
        var results = new List<NodeDefinition>();

        foreach (var assembly in assemblies.Where(a => a is not null && !a.IsDynamic))
        {
            foreach (var type in SafeGetTypes(assembly))
            {
                if (type is null || type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                if (!IsContextType(type))
                {
                    continue;
                }

                foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    var attribute = method.GetCustomAttribute<NodeAttribute>();
                    if (attribute is null)
                    {
                        continue;
                    }

                    var definition = BuildDefinition(type, method, attribute);
                    results.Add(definition);
                }
            }
        }

        return results;
    }

    private static bool IsContextType(Type type)
    {
        return typeof(INodeContext).IsAssignableFrom(type)
               || typeof(INodeMethodContext).IsAssignableFrom(type);
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!
                .Cast<Type>();
        }
    }

    private static NodeDefinition BuildDefinition(Type contextType, MethodInfo method, NodeAttribute attribute)
    {
        var inputs = new List<SocketData>();
        var outputs = new List<SocketData>();

        var name = string.IsNullOrWhiteSpace(attribute.Name) ? method.Name : attribute.Name;
        var category = !string.IsNullOrWhiteSpace(attribute.Category)
            ? attribute.Category
            : string.IsNullOrWhiteSpace(attribute.Menu) ? "General" : attribute.Menu;
        var description = attribute.Description ?? string.Empty;

        if (attribute.IsCallable)
        {
            if (!attribute.IsExecutionInitiator)
            {
                AddSocketIfMissing(inputs, CreateExecutionSocket("Enter", isInput: true));
            }

            AddSocketIfMissing(outputs, CreateExecutionSocket("Exit", isInput: false));
        }

        var parameters = method.GetParameters();
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];

            if (parameter.ParameterType == typeof(CancellationToken))
            {
                continue;
            }

            var paramType = parameter.ParameterType;
            var isByRef = paramType.IsByRef;
            if (isByRef)
            {
                paramType = paramType.GetElementType() ?? paramType;
            }

            var isOut = parameter.IsOut || isByRef;
            var socketName = string.IsNullOrWhiteSpace(parameter.Name) ? $"arg{i}" : parameter.Name;
            var socket = CreateSocket(socketName, paramType, isInput: !isOut);

            if (isOut)
            {
                AddSocketIfMissing(outputs, socket with { IsInput = false });
            }
            else
            {
                AddSocketIfMissing(inputs, socket with { IsInput = true });
            }
        }

        var id = BuildDefinitionId(contextType, method);
        var inputsSnapshot = inputs.ToArray();
        var outputsSnapshot = outputs.ToArray();

        return new NodeDefinition(
            id,
            name,
            category,
            description,
            inputsSnapshot,
            outputsSnapshot,
            () => new NodeData(
                Guid.NewGuid().ToString("N"),
                name,
                attribute.IsCallable,
                attribute.IsExecutionInitiator,
                inputsSnapshot,
                outputsSnapshot,
                id));
    }

    private static SocketData CreateSocket(string name, Type type, bool isInput)
    {
        var typeName = type.FullName ?? type.Name;
        var isExecution = type == typeof(ExecutionPath);
        return new SocketData(name, typeName, isInput, isExecution);
    }

    private static SocketData CreateExecutionSocket(string name, bool isInput)
    {
        var typeName = typeof(ExecutionPath).FullName ?? nameof(ExecutionPath);
        return new SocketData(name, typeName, isInput, true);
    }

    private static void AddSocketIfMissing(List<SocketData> sockets, SocketData socket)
    {
        if (sockets.Any(s => s.Name.Equals(socket.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        sockets.Add(socket);
    }

    private static string BuildDefinitionId(Type contextType, MethodInfo method)
    {
        var parameters = method.GetParameters()
            .Select(p => p.ParameterType.IsByRef
                ? p.ParameterType.GetElementType() ?? p.ParameterType
                : p.ParameterType)
            .Select(t => t.FullName ?? t.Name);

        var signature = string.Join(",", parameters);
        return $"{contextType.FullName}.{method.Name}({signature})";
    }
}
