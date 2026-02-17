using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

    internal static string ResolveCategory(string menu, string category)
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
