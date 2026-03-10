using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SyncForge.Abstractions.Configuration;
using SyncForge.Abstractions.Connectors;
using SyncForge.Abstractions.Logging;
using SyncForge.Core.Mapping;
using SyncForge.Core.Orchestration;

namespace SyncForge.Configurator.Services;

public static class DryRunExecutionService
{
    public static async Task<ExecutionServiceResult> ExecuteAsync(
        JobDefinition definition,
        string? currentFilePath,
        string pluginDirectory,
        bool dryRun,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        var baseDirectory = ResolveBaseDirectory(currentFilePath);
        DotEnvLoader.TryLoad(baseDirectory);
        SecretResolver.ResolveInPlace(definition);
        NormalizePathSettings(definition, baseDirectory);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<ISyncForgeLogger>(_ => new CallbackSyncForgeLogger(log));
        serviceCollection.AddSingleton<IMappingEngine, MappingEngine>();
        serviceCollection.AddTransient<IJobOrchestrator, JobOrchestrator>();

        var pluginRegistry = LoadPlugins(serviceCollection, GetPluginAssembliesToLoad(definition), pluginDirectory);
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var resolver = new DynamicConnectorResolver(serviceProvider, pluginRegistry);

        var sourceConnector = resolver.ResolveSource(definition);
        var targetConnector = resolver.ResolveTarget(definition);
        var orchestrator = serviceProvider.GetRequiredService<IJobOrchestrator>();

        var result = await orchestrator.ExecuteAsync(
            definition,
            sourceConnector,
            targetConnector,
            dryRun,
            cancellationToken);

        var summary = new
        {
            job = result.JobName,
            mode = dryRun ? "dry-run" : "run",
            dryRun,
            startedAtUtc = result.StartedAtUtc,
            completedAtUtc = result.CompletedAtUtc,
            durationMs = (long)(result.CompletedAtUtc - result.StartedAtUtc).TotalMilliseconds,
            processed = result.WriteResult.ProcessedRecords,
            succeeded = result.WriteResult.SucceededRecords,
            failed = result.WriteResult.FailedRecords,
            message = result.WriteResult.Message,
            targetStats = result.WriteResult.Stats
        };

        return new ExecutionServiceResult
        {
            SummaryJson = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true })
        };
    }

    private static string ResolveBaseDirectory(string? currentFilePath)
    {
        if (string.IsNullOrWhiteSpace(currentFilePath))
        {
            return Directory.GetCurrentDirectory();
        }

        return Path.GetDirectoryName(Path.GetFullPath(currentFilePath))
            ?? Directory.GetCurrentDirectory();
    }

    private static IReadOnlyList<string> GetPluginAssembliesToLoad(JobDefinition definition)
    {
        var assemblies = new List<string>();

        if (!string.Equals(definition.Source.Type, "jsonl", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(definition.Source.Plugin))
        {
            assemblies.Add(definition.Source.Plugin);
        }

        if (!string.Equals(definition.Target.Type, "jsonl", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(definition.Target.Plugin))
        {
            assemblies.Add(definition.Target.Plugin);
        }

        return assemblies;
    }

    private static void NormalizePathSettings(JobDefinition definition, string baseDirectory)
    {
        NormalizePath(definition.Source.Settings, baseDirectory);
        NormalizePath(definition.Target.Settings, baseDirectory);
    }

    private static void NormalizePath(Dictionary<string, string?> settings, string baseDirectory)
    {
        if (!settings.TryGetValue("path", out var rawPath) || string.IsNullOrWhiteSpace(rawPath))
        {
            return;
        }

        settings["path"] = Path.IsPathRooted(rawPath)
            ? rawPath
            : Path.GetFullPath(Path.Combine(baseDirectory, rawPath));
    }

    private static PluginRegistry LoadPlugins(
        IServiceCollection services,
        IEnumerable<string> pluginAssemblyNames,
        string pluginDirectory)
    {
        var registry = new PluginRegistry();

        foreach (var pluginAssemblyName in pluginAssemblyNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var assemblyPath = ResolveAssemblyPath(pluginAssemblyName, pluginDirectory);
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);

            foreach (var type in assembly.GetTypes().Where(IsConcreteConnectorType))
            {
                if (typeof(ISourceConnector).IsAssignableFrom(type))
                {
                    services.AddTransient(type);
                    registry.SourceConnectorTypes.Add(type);
                }

                if (typeof(ITargetConnector).IsAssignableFrom(type))
                {
                    services.AddTransient(type);
                    registry.TargetConnectorTypes.Add(type);
                }
            }
        }

        return registry;
    }

    private static string ResolveAssemblyPath(string assemblyName, string pluginDirectory)
    {
        var directPath = Path.Combine(pluginDirectory, $"{assemblyName}.dll");
        if (File.Exists(directPath))
        {
            return directPath;
        }

        var candidates = EnumeratePluginRoots(pluginDirectory)
            .SelectMany(root =>
            {
                try
                {
                    return Directory.EnumerateFiles(root, $"{assemblyName}.dll", SearchOption.AllDirectories);
                }
                catch
                {
                    return [];
                }
            })
            .Where(path =>
                !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && !path.Contains(Path.DirectorySeparatorChar + "ref" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidates.Count > 0)
        {
            return candidates[0];
        }

        throw new FileNotFoundException(
            $"Plugin assembly '{assemblyName}' was not found under '{pluginDirectory}'.",
            directPath);
    }

    private static IEnumerable<string> EnumeratePluginRoots(string pluginDirectory)
    {
        if (Directory.Exists(pluginDirectory))
        {
            yield return pluginDirectory;
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 6 && current is not null; i++)
        {
            var candidateSrc = Path.Combine(current.FullName, "src");
            if (Directory.Exists(candidateSrc))
            {
                yield return candidateSrc;
                yield break;
            }

            current = current.Parent;
        }
    }

    private static bool IsConcreteConnectorType(Type type)
    {
        return type.IsClass && !type.IsAbstract && type.IsPublic;
    }

    private sealed class PluginRegistry
    {
        public List<Type> SourceConnectorTypes { get; } = [];

        public List<Type> TargetConnectorTypes { get; } = [];
    }

    private sealed class DynamicConnectorResolver
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly PluginRegistry _plugins;

        public DynamicConnectorResolver(IServiceProvider serviceProvider, PluginRegistry plugins)
        {
            _serviceProvider = serviceProvider;
            _plugins = plugins;
        }

        public ISourceConnector ResolveSource(JobDefinition definition)
        {
            if (string.Equals(definition.Source.Type, "jsonl", StringComparison.OrdinalIgnoreCase))
            {
                if (!definition.Source.Settings.TryGetValue("path", out var path) || string.IsNullOrWhiteSpace(path))
                {
                    throw new InvalidOperationException("Source setting 'path' is required for source.type 'jsonl'.");
                }

                return new JsonLinesSourceConnector(path);
            }

            var candidates = _plugins.SourceConnectorTypes
                .Where(type => string.Equals(type.Assembly.GetName().Name, definition.Source.Plugin, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var connectorType = PickConnectorType(candidates, definition.Source.Type, "SourceConnector");
            return (ISourceConnector)ActivatorUtilities.CreateInstance(_serviceProvider, connectorType);
        }

        public ITargetConnector ResolveTarget(JobDefinition definition)
        {
            if (string.Equals(definition.Target.Type, "jsonl", StringComparison.OrdinalIgnoreCase))
            {
                if (!definition.Target.Settings.TryGetValue("path", out var path) || string.IsNullOrWhiteSpace(path))
                {
                    throw new InvalidOperationException("Target setting 'path' is required for target.type 'jsonl'.");
                }

                return new JsonLinesTargetConnector(path);
            }

            var candidates = _plugins.TargetConnectorTypes
                .Where(type => string.Equals(type.Assembly.GetName().Name, definition.Target.Plugin, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var connectorType = PickConnectorType(candidates, definition.Target.Type, "TargetConnector");
            return (ITargetConnector)ActivatorUtilities.CreateInstance(_serviceProvider, connectorType);
        }

        private static Type PickConnectorType(IReadOnlyCollection<Type> candidates, string configuredType, string suffix)
        {
            if (candidates.Count == 0)
            {
                throw new InvalidOperationException($"No connector implementation found for configured type '{configuredType}'.");
            }

            if (candidates.Count == 1)
            {
                return candidates.Single();
            }

            var normalizedType = Normalize(configuredType);
            var match = candidates.FirstOrDefault(type =>
                Normalize(type.Name).Contains(normalizedType, StringComparison.OrdinalIgnoreCase)
                && Normalize(type.Name).Contains(Normalize(suffix), StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                return match;
            }

            throw new InvalidOperationException(
                $"Multiple connector implementations found for '{configuredType}', but no unique match by naming convention.");
        }

        private static string Normalize(string value)
        {
            var chars = value.Where(char.IsLetterOrDigit).ToArray();
            return new string(chars).ToLowerInvariant();
        }
    }

    private sealed class CallbackSyncForgeLogger : ISyncForgeLogger
    {
        private readonly Action<string> _log;

        public CallbackSyncForgeLogger(Action<string> log)
        {
            _log = log;
        }

        public void Info(string messageTemplate, params object[] args)
        {
            _log("INFO  " + RenderMessage(messageTemplate, args));
        }

        public void Warning(string messageTemplate, params object[] args)
        {
            _log("WARN  " + RenderMessage(messageTemplate, args));
        }

        public void Error(string messageTemplate, params object[] args)
        {
            _log("ERROR " + RenderMessage(messageTemplate, args));
        }

        public void Error(Exception exception, string messageTemplate, params object[] args)
        {
            _log("ERROR " + RenderMessage(messageTemplate, args));
            _log($"ERROR {exception.GetType().Name}: {exception.Message}");
            if (!string.IsNullOrWhiteSpace(exception.StackTrace))
            {
                _log(exception.StackTrace);
            }
        }

        private static string RenderMessage(string template, object[] args)
        {
            var rendered = template;
            for (var i = 0; i < args.Length; i++)
            {
                var start = rendered.IndexOf('{');
                var end = rendered.IndexOf('}', start + 1);
                if (start < 0 || end <= start)
                {
                    break;
                }

                var token = rendered.Substring(start, end - start + 1);
                rendered = rendered.Replace(token, args[i]?.ToString(), StringComparison.Ordinal);
            }

            return rendered;
        }
    }

    private static class DotEnvLoader
    {
        public static void TryLoad(string baseDirectory)
        {
            var path = Path.Combine(baseDirectory, ".env");
            if (!File.Exists(path))
            {
                return;
            }

            foreach (var line in File.ReadAllLines(path))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                {
                    continue;
                }

                var sep = trimmed.IndexOf('=');
                if (sep <= 0)
                {
                    continue;
                }

                var key = trimmed[..sep].Trim();
                var value = trimmed[(sep + 1)..].Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
        }
    }

    private static class SecretResolver
    {
        public static void ResolveInPlace(JobDefinition definition)
        {
            ResolveSettings(definition.Source.Settings);
            ResolveSettings(definition.Target.Settings);
        }

        private static void ResolveSettings(Dictionary<string, string?> settings)
        {
            var keys = settings.Keys.ToList();
            foreach (var key in keys)
            {
                settings[key] = ResolveValue(settings[key]);
            }
        }

        private static string? ResolveValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            if (!value.StartsWith("${ENV:", StringComparison.Ordinal) || !value.EndsWith('}'))
            {
                return value;
            }

            var envName = value.Substring(6, value.Length - 7).Trim();
            var envValue = Environment.GetEnvironmentVariable(envName);
            if (string.IsNullOrWhiteSpace(envValue))
            {
                throw new InvalidOperationException($"Environment variable '{envName}' is required but not set.");
            }

            return envValue;
        }
    }
}

public sealed class ExecutionServiceResult
{
    public required string SummaryJson { get; init; }
}
