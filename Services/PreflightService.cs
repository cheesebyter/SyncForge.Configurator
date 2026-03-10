using SyncForge.Abstractions.Configuration;
using SyncForge.Configurator.ViewModels;

namespace SyncForge.Configurator.Services;

public static class PreflightService
{
    public static async Task<IReadOnlyList<PreflightFinding>> RunAsync(
        JobDefinition definition,
        string? currentFilePath,
        string pluginDirectory,
        IReadOnlyList<ConnectorDescriptor> sourceConnectors,
        IReadOnlyList<ConnectorDescriptor> targetConnectors,
        CancellationToken cancellationToken)
    {
        var findings = new List<PreflightFinding>();

        AddConnectorResolutionFindings(findings, definition, sourceConnectors, targetConnectors);
        AddSettingFindings(findings, definition);

        await AddSourceProbeFindingsAsync(findings, definition, currentFilePath, cancellationToken);
        await AddPipelineProbeFindingsAsync(findings, definition, currentFilePath, pluginDirectory, cancellationToken);

        if (!findings.Any(item => string.Equals(item.Severity, "ERROR", StringComparison.OrdinalIgnoreCase)))
        {
            findings.Add(new PreflightFinding
            {
                Severity = "INFO",
                Scope = "Summary",
                Message = "No blocking preflight errors detected."
            });
        }

        return findings;
    }

    private static void AddConnectorResolutionFindings(
        ICollection<PreflightFinding> findings,
        JobDefinition definition,
        IReadOnlyList<ConnectorDescriptor> sourceConnectors,
        IReadOnlyList<ConnectorDescriptor> targetConnectors)
    {
        var sourceFound = sourceConnectors.Any(item =>
            string.Equals(item.ConnectorType, definition.Source.Type, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.AssemblyName, definition.Source.Plugin, StringComparison.OrdinalIgnoreCase));

        if (!sourceFound)
        {
            findings.Add(new PreflightFinding
            {
                Severity = "ERROR",
                Scope = "Source",
                Message = $"Connector '{definition.Source.Type}' from plugin '{definition.Source.Plugin}' was not discovered."
            });
        }

        var targetFound = targetConnectors.Any(item =>
            string.Equals(item.ConnectorType, definition.Target.Type, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.AssemblyName, definition.Target.Plugin, StringComparison.OrdinalIgnoreCase));

        if (!targetFound)
        {
            findings.Add(new PreflightFinding
            {
                Severity = "ERROR",
                Scope = "Target",
                Message = $"Connector '{definition.Target.Type}' from plugin '{definition.Target.Plugin}' was not discovered."
            });
        }
    }

    private static void AddSettingFindings(ICollection<PreflightFinding> findings, JobDefinition definition)
    {
        ValidateRequiredSettings(findings, "Source", definition.Source.Type, definition.Source.Settings);
        ValidateRequiredSettings(findings, "Target", definition.Target.Type, definition.Target.Settings);
    }

    private static async Task AddSourceProbeFindingsAsync(
        ICollection<PreflightFinding> findings,
        JobDefinition definition,
        string? currentFilePath,
        CancellationToken cancellationToken)
    {
        try
        {
            var columns = await SourcePreviewService.LoadColumnsAsync(definition, currentFilePath);
            cancellationToken.ThrowIfCancellationRequested();

            findings.Add(new PreflightFinding
            {
                Severity = "INFO",
                Scope = "Source",
                Message = $"Source probe succeeded. Columns detected: {columns.Count}."
            });
        }
        catch (Exception ex)
        {
            findings.Add(new PreflightFinding
            {
                Severity = "ERROR",
                Scope = "Source",
                Message = "Source probe failed: " + ex.Message
            });
        }
    }

    private static async Task AddPipelineProbeFindingsAsync(
        ICollection<PreflightFinding> findings,
        JobDefinition definition,
        string? currentFilePath,
        string pluginDirectory,
        CancellationToken cancellationToken)
    {
        try
        {
            var cloned = Clone(definition);

            // Dry-run probe validates end-to-end orchestration path without committing writes.
            await DryRunExecutionService.ExecuteAsync(
                cloned,
                currentFilePath,
                pluginDirectory,
                dryRun: true,
                _ => { },
                cancellationToken);

            findings.Add(new PreflightFinding
            {
                Severity = "INFO",
                Scope = "Pipeline",
                Message = "Dry-run preflight probe succeeded."
            });

            findings.Add(new PreflightFinding
            {
                Severity = "WARN",
                Scope = "Target",
                Message = "Dry-run preflight does not guarantee target write permissions/connectivity for all connector implementations."
            });
        }
        catch (Exception ex)
        {
            findings.Add(new PreflightFinding
            {
                Severity = "ERROR",
                Scope = "Pipeline",
                Message = "Dry-run preflight probe failed: " + ex.Message
            });
        }
    }

    private static void ValidateRequiredSettings(
        ICollection<PreflightFinding> findings,
        string scope,
        string connectorType,
        IReadOnlyDictionary<string, string?> settings)
    {
        foreach (var key in GetRequiredKeys(connectorType, scope))
        {
            if (settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            findings.Add(new PreflightFinding
            {
                Severity = "ERROR",
                Scope = scope,
                Message = $"Missing required setting '{key}' for {scope.ToLowerInvariant()} type '{connectorType}'."
            });
        }
    }

    private static IReadOnlyList<string> GetRequiredKeys(string connectorType, string scope)
    {
        var normalizedType = Normalize(connectorType);
        var normalizedScope = Normalize(scope);

        if (normalizedScope == "source")
        {
            return normalizedType switch
            {
                "csv" => ["path"],
                "xlsx" => ["path"],
                "jsonl" => ["path"],
                "rest" => ["url"],
                _ => []
            };
        }

        if (normalizedScope == "target")
        {
            return normalizedType switch
            {
                "jsonl" => ["path"],
                "rest" => ["url"],
                "mssql" => ["connectionString", "table"],
                _ => []
            };
        }

        return [];
    }

    private static JobDefinition Clone(JobDefinition definition)
    {
        var json = JobDefinitionJson.Serialize(definition);
        return JobDefinitionJson.Deserialize(json);
    }

    private static string Normalize(string value)
    {
        var chars = value.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars).ToLowerInvariant();
    }
}
