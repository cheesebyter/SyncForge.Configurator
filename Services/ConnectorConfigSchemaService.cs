namespace SyncForge.Configurator.Services;

public static class ConnectorConfigSchemaService
{
    public static IReadOnlyList<string> GetSuggestedKeys(string connectorType, string kind)
    {
        var normalizedType = Normalize(connectorType);
        var normalizedKind = Normalize(kind);

        if (normalizedKind == "source")
        {
            return normalizedType switch
            {
                "csv" => ["path", "delimiter", "encoding", "hasHeader", "quote", "escape"],
                "xlsx" => ["path", "sheetName", "maxRows", "maxFileSizeBytes"],
                "rest" => ["url", "jsonPath", "timeoutSeconds", "header.Authorization"],
                "jsonl" => ["path"],
                _ => []
            };
        }

        if (normalizedKind == "target")
        {
            return normalizedType switch
            {
                "mssql" => ["connectionString", "table", "batchSize", "commandTimeoutSeconds", "replaceMode", "constraintStrategy", "upsertImplementation"],
                "rest" => ["url", "mode", "batchSize", "timeoutSeconds", "header.Authorization"],
                "jsonl" => ["path"],
                _ => []
            };
        }

        return [];
    }

    private static string Normalize(string value)
    {
        var chars = value.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars).ToLowerInvariant();
    }
}
