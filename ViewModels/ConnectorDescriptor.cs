namespace SyncForge.Configurator.ViewModels;

public sealed class ConnectorDescriptor
{
    public required string AssemblyName { get; init; }

    public required string ConnectorType { get; init; }

    public required string ClassName { get; init; }

    public required string Kind { get; init; }

    public string DisplayName => $"{ConnectorType} ({AssemblyName})";

    public override string ToString()
    {
        return DisplayName;
    }
}
