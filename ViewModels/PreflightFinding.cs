using System;

namespace SyncForge.Configurator.ViewModels;

public sealed class PreflightFinding
{
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    public string Severity { get; init; } = "INFO";

    public string Scope { get; init; } = "General";

    public string Message { get; init; } = string.Empty;

    public string Rendered => $"[{Severity}] {Scope}: {Message}";
}
