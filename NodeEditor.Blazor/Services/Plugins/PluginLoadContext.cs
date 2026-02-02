using System.Reflection;
using System.Runtime.Loader;

namespace NodeEditor.Blazor.Services.Plugins;

public sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var defaultAssembly = AssemblyLoadContext.Default.Assemblies
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase));
        if (defaultAssembly is not null)
        {
            return defaultAssembly;
        }

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath is null)
        {
            return null;
        }

        return LoadFromAssemblyPath(assemblyPath);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath is null)
        {
            return IntPtr.Zero;
        }

        return LoadUnmanagedDllFromPath(libraryPath);
    }
}
