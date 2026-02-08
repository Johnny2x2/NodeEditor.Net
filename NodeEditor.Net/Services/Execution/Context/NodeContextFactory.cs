using System.Reflection;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution;

public interface INodeContextFactory
{
    CompositeNodeContext CreateCompositeFromLoadedAssemblies();
}

public sealed class NodeContextFactory : INodeContextFactory
{
    public CompositeNodeContext CreateCompositeFromLoadedAssemblies()
    {
        var contexts = new List<object>();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a => a is not null && !a.IsDynamic))
        {
            foreach (var type in SafeGetTypes(assembly))
            {
                if (type is null || type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                if (!typeof(INodeContext).IsAssignableFrom(type)
                    && !typeof(INodeMethodContext).IsAssignableFrom(type))
                {
                    continue;
                }

                if (type.GetConstructor(Type.EmptyTypes) is null)
                {
                    continue;
                }

                try
                {
                    var instance = Activator.CreateInstance(type);
                    if (instance is not null)
                    {
                        contexts.Add(instance);
                    }
                }
                catch
                {
                    // Ignore contexts that cannot be created.
                }
            }
        }

        return new CompositeNodeContext(contexts);
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!.Cast<Type>();
        }
    }
}
