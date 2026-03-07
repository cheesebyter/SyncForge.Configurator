namespace SyncForge.Configurator.ViewModels;

public sealed class LogEntry
{
    public required DateTime Timestamp { get; init; }

    public required string Level { get; init; }

    public required string Source { get; init; }

    public required string Message { get; init; }

    public string Rendered => $"[{Timestamp:HH:mm:ss}] [{Level}] [{Source}] {Message}";
}
