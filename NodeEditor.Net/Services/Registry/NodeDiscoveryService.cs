using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Execution;

namespace NodeEditor.Net.Services.Registry;

public sealed class NodeDiscoveryService
{
    public IReadOnlyList<NodeDefinition> DiscoverFromAssemblies(IEnumerable<Assembly> assemblies)
    {
        var definitions = new List<NodeDefinition>();

        foreach (var assembly in assemblies)
        {
            if (assembly is null || assembly.IsDynamic)
            {
                continue;
            }

            foreach (var type in SafeGetTypes(assembly))
            {
                if (type is null || type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                if (type.IsSubclassOf(typeof(NodeBase)))
                {
                    if (type.GetConstructor(Type.EmptyTypes) is null)
                    {
                        continue;
                    }

                    try
                    {
                        var definition = BuildDefinitionFromType(type);
                        if (definition is not null)
                        {
                            definitions.Add(definition);
                        }
                    }
                    catch
                    {
                        // Skip types that cannot be instantiated or configured.
                    }
                }

                if (IsContextType(type))
                {
                    definitions.AddRange(BuildDefinitionsFromContext(type));
                }
            }
        }

        return definitions;
    }

    public NodeDefinition? BuildDefinitionFromType(Type nodeType)
    {
        if (nodeType.IsAbstract || !nodeType.IsSubclassOf(typeof(NodeBase)))
        {
            return null;
        }

        var instance = (NodeBase)Activator.CreateInstance(nodeType)!;
        try
        {
            var builder = NodeBuilder.CreateForType(nodeType);
            instance.Configure(builder);
            return builder.Build();
        }
        finally
        {
            instance.OnDisposed();
        }
    }

    private static bool IsContextType(Type type)
    {
        return typeof(INodeContext).IsAssignableFrom(type)
               || typeof(INodeMethodContext).IsAssignableFrom(type);
    }

    private static IEnumerable<NodeDefinition> BuildDefinitionsFromContext(Type contextType)
    {
        var results = new List<NodeDefinition>();

        foreach (var method in contextType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var attribute = method.GetCustomAttribute<NodeAttribute>();
            if (attribute is null)
            {
                continue;
            }

            var definition = BuildDefinition(contextType, method, attribute);
            results.Add(definition);
        }

        return results;
    }

    private static NodeDefinition BuildDefinition(Type contextType, MethodInfo method, NodeAttribute attribute)
    {
        var inputs = new List<SocketData>();
        var outputs = new List<SocketData>();

        var name = string.IsNullOrWhiteSpace(attribute.Name) ? method.Name : attribute.Name;
        var menu = attribute.Menu ?? string.Empty;
        var category = attribute.Category ?? string.Empty;
        var resolvedCategory = ResolveCategory(menu, category);
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
            var editorHint = BuildEditorHint(parameter);
            var socket = CreateSocket(socketName, paramType, isInput: !isOut, editorHint);

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
            resolvedCategory,
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

    private static string ResolveCategory(string menu, string category)
    {
        var hasMenu = !string.IsNullOrWhiteSpace(menu);
        var hasCategory = !string.IsNullOrWhiteSpace(category);

        if (hasMenu && hasCategory)
        {
            if (string.Equals(menu, category, StringComparison.OrdinalIgnoreCase))
            {
                return category;
            }

            if (string.Equals(category, "General", StringComparison.OrdinalIgnoreCase))
            {
                return menu;
            }

            if (string.Equals(category, "Basic", StringComparison.OrdinalIgnoreCase)
                && menu.Contains('/', StringComparison.Ordinal))
            {
                return menu;
            }

            return $"{menu}/{category}";
        }

        if (hasCategory)
        {
            return category;
        }

        if (hasMenu)
        {
            return menu;
        }

        return "General";
    }

    private static SocketData CreateSocket(string name, Type type, bool isInput, SocketEditorHint? editorHint = null)
    {
        var typeName = type.FullName ?? type.Name;
        var isExecution = type == typeof(ExecutionPath);
        return new SocketData(name, typeName, isInput, isExecution, EditorHint: editorHint);
    }

    private static SocketData CreateExecutionSocket(string name, bool isInput)
    {
        var typeName = typeof(ExecutionPath).FullName ?? nameof(ExecutionPath);
        return new SocketData(name, typeName, isInput, true);
    }

    private static SocketEditorHint? BuildEditorHint(ParameterInfo parameter)
    {
        var attribute = parameter.GetCustomAttribute<SocketEditorAttribute>();
        if (attribute is null)
        {
            return null;
        }

        var min = double.IsNaN(attribute.Min) ? (double?)null : attribute.Min;
        var max = double.IsNaN(attribute.Max) ? (double?)null : attribute.Max;
        var step = double.IsNaN(attribute.Step) ? (double?)null : attribute.Step;

        return new SocketEditorHint(
            attribute.Kind,
            attribute.Options,
            min,
            max,
            step,
            attribute.Placeholder,
            attribute.Label);
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
}
