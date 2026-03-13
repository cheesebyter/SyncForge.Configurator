using System.Reflection;
using SyncForge.Abstractions.Connectors;
using SyncForge.Configurator.ViewModels;

namespace SyncForge.Configurator.Services;

public static class ConnectorDiscoveryService
{
    public static IReadOnlyList<ConnectorDescriptor> Discover(string pluginDirectory, string? currentFilePath = null)
    {
        var descriptors = new List<ConnectorDescriptor>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dllPath in EnumeratePluginAssemblies(pluginDirectory, currentFilePath))
        {
            try
            {
                var loadContext = new PluginAssemblyLoadContext(dllPath);
                var assembly = loadContext.LoadFromAssemblyPath(dllPath);
                foreach (var type in GetLoadableTypes(assembly).Where(IsConcreteType))
                {
                    if (typeof(ISourceConnector).IsAssignableFrom(type))
                    {
                        Add(descriptors, seen, type, "Source");
                    }

                    if (typeof(ITargetConnector).IsAssignableFrom(type))
                    {
                        Add(descriptors, seen, type, "Target");
                    }
                }
            }
            catch
            {
                // Ignore non-loadable plugin candidates to keep UI resilient.
            }
        }

        return descriptors
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.ConnectorType)
            .ThenBy(item => item.AssemblyName)
            .ToList();
    }

    private static void Add(
        ICollection<ConnectorDescriptor> output,
        ISet<string> seen,
        Type type,
        string kind)
    {
        var assemblyName = type.Assembly.GetName().Name;
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return;
        }

        var connectorType = ExtractConnectorType(type.Name, kind);
        if (string.IsNullOrWhiteSpace(connectorType))
        {
            return;
        }

        var key = $"{kind}|{assemblyName}|{connectorType}|{type.FullName}";
        if (!seen.Add(key))
        {
            return;
        }

        output.Add(new ConnectorDescriptor
        {
            AssemblyName = assemblyName,
            ConnectorType = connectorType,
            ClassName = type.FullName ?? type.Name,
            Kind = kind
        });
    }

    private static bool IsConcreteType(Type type)
    {
        return type.IsClass && !type.IsAbstract && type.IsPublic;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null).Cast<Type>();
        }
    }

    private static string ExtractConnectorType(string className, string kind)
    {
        var suffix = kind + "Connector";
        var normalized = className.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? className[..^suffix.Length]
            : className;

        var chars = normalized.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars).ToLowerInvariant();
    }

    private static IEnumerable<string> EnumeratePluginAssemblies(string pluginDirectory, string? currentFilePath)
    {
        var resolvedPluginDirectory = ResolvePluginDirectory(pluginDirectory, currentFilePath);
        var roots = new List<string>();

        if (!string.IsNullOrWhiteSpace(pluginDirectory))
        {
            if (Directory.Exists(resolvedPluginDirectory))
            {
                roots.Add(resolvedPluginDirectory);
            }
        }
        else
        {
            var baseDirectory = ResolvePluginDirectory(pluginDirectory, currentFilePath);
            if (Directory.Exists(baseDirectory))
            {
                roots.Add(baseDirectory);
            }

            var current = new DirectoryInfo(AppContext.BaseDirectory);
            for (var i = 0; i < 6 && current is not null; i++)
            {
                var candidateSrc = Path.Combine(current.FullName, "src");
                if (Directory.Exists(candidateSrc))
                {
                    roots.Add(candidateSrc);
                    break;
                }

                current = current.Parent;
            }
        }

        var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(root, "SyncForge.Plugin.*.dll", SearchOption.AllDirectories);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                if (file.Contains(Path.DirectorySeparatorChar + "ref" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    || file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fullPath = Path.GetFullPath(file);
                if (seenFiles.Add(fullPath))
                {
                    yield return fullPath;
                }
            }
        }
    }

    public static string ResolvePluginDirectory(string pluginDirectory, string? currentFilePath)
    {
        if (string.IsNullOrWhiteSpace(pluginDirectory))
        {
            return AppContext.BaseDirectory;
        }

        if (Path.IsPathRooted(pluginDirectory))
        {
            return Path.GetFullPath(pluginDirectory);
        }

        var anchorDirectory = AppContext.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(currentFilePath))
        {
            anchorDirectory = Path.GetDirectoryName(Path.GetFullPath(currentFilePath)) ?? AppContext.BaseDirectory;
        }

        return Path.GetFullPath(Path.Combine(anchorDirectory, pluginDirectory));
    }
}
