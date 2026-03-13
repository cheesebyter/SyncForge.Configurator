using System.Reflection;
using System.Runtime.Loader;

namespace SyncForge.Configurator.Services;

internal sealed class PluginAssemblyLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginAssemblyLoadContext(string pluginAssemblyPath)
        : base($"SyncForge.Configurator.Plugin::{Path.GetFileNameWithoutExtension(pluginAssemblyPath)}", isCollectible: false)
    {
        _resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var alreadyLoaded = Default.Assemblies.FirstOrDefault(item =>
            string.Equals(item.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase));
        if (alreadyLoaded is not null)
        {
            return alreadyLoaded;
        }

        var resolvedAssemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (resolvedAssemblyPath is null)
        {
            return null;
        }

        return LoadFromAssemblyPath(resolvedAssemblyPath);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var resolvedPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (resolvedPath is null)
        {
            return 0;
        }

        return LoadUnmanagedDllFromPath(resolvedPath);
    }
}
