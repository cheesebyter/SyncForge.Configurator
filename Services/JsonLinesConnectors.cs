using System.Text.Json;
using SyncForge.Abstractions.Connectors;
using SyncForge.Abstractions.Models;

namespace SyncForge.Configurator.Services;

public sealed class JsonLinesSourceConnector : ISourceConnector
{
    private readonly string _filePath;

    public JsonLinesSourceConnector(string filePath)
    {
        _filePath = filePath;
    }

    public Task<IAsyncEnumerable<DataRecord>> ReadAsync(JobContext context)
    {
        if (!File.Exists(_filePath))
        {
            throw new FileNotFoundException($"Source JSONL file not found: {_filePath}", _filePath);
        }

        return Task.FromResult(ReadInternalAsync());
    }

    private async IAsyncEnumerable<DataRecord> ReadInternalAsync()
    {
        using var stream = File.OpenRead(_filePath);
        using var reader = new StreamReader(stream);

        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var document = JsonDocument.Parse(line);
            var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                fields[property.Name] = JsonValueConverter.ToObject(property.Value);
            }

            yield return new DataRecord { Fields = fields };
        }
    }
}

public sealed class JsonLinesTargetConnector : ITargetConnector
{
    private readonly string _filePath;

    public JsonLinesTargetConnector(string filePath)
    {
        _filePath = filePath;
    }

    public async Task<WriteResult> WriteAsync(IAsyncEnumerable<DataRecord> records, JobContext context)
    {
        if (context.DryRun)
        {
            long dryRunCount = 0;
            await foreach (var _ in records)
            {
                dryRunCount++;
            }

            return new WriteResult
            {
                ProcessedRecords = dryRunCount,
                SucceededRecords = dryRunCount,
                FailedRecords = 0,
                Message = "Dry-run: no output written.",
                Stats = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
                {
                    ["processed"] = dryRunCount,
                    ["succeeded"] = dryRunCount,
                    ["failed"] = 0
                }
            };
        }

        var targetDirectory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        var strategyMode = context.StrategyMode;
        if (string.Equals(strategyMode, "Replace", StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }

        var outputRecords = await BuildOutputRecordsAsync(records, strategyMode, context.StrategyKeyFields);
        await File.WriteAllLinesAsync(_filePath, outputRecords);

        return new WriteResult
        {
            ProcessedRecords = outputRecords.Count,
            SucceededRecords = outputRecords.Count,
            FailedRecords = 0,
            Message = $"Wrote {outputRecords.Count} record(s) to {_filePath}.",
            Stats = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
            {
                ["processed"] = outputRecords.Count,
                ["succeeded"] = outputRecords.Count,
                ["failed"] = 0,
                ["inserted"] = outputRecords.Count
            }
        };
    }

    private async Task<List<string>> BuildOutputRecordsAsync(
        IAsyncEnumerable<DataRecord> records,
        string strategyMode,
        IReadOnlyList<string> strategyKeyFields)
    {
        if (string.Equals(strategyMode, "UpsertByKey", StringComparison.OrdinalIgnoreCase) && strategyKeyFields.Count > 0)
        {
            var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(_filePath))
            {
                foreach (var existingLine in await File.ReadAllLinesAsync(_filePath))
                {
                    if (string.IsNullOrWhiteSpace(existingLine))
                    {
                        continue;
                    }

                    var existingRecord = ParseJsonLineToRecord(existingLine);
                    var existingKey = BuildCompositeKey(existingRecord, strategyKeyFields);
                    merged[existingKey] = existingLine;
                }
            }

            await foreach (var record in records)
            {
                var key = BuildCompositeKey(record, strategyKeyFields);
                var jsonLine = JsonSerializer.Serialize(record.Fields);
                merged[key] = jsonLine;
            }

            return merged.Values.ToList();
        }

        var lines = new List<string>();

        if (string.Equals(strategyMode, "InsertOnly", StringComparison.OrdinalIgnoreCase) && File.Exists(_filePath))
        {
            lines.AddRange(await File.ReadAllLinesAsync(_filePath));
        }

        await foreach (var record in records)
        {
            lines.Add(JsonSerializer.Serialize(record.Fields));
        }

        return lines;
    }

    private static DataRecord ParseJsonLineToRecord(string jsonLine)
    {
        using var doc = JsonDocument.Parse(jsonLine);
        var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            fields[property.Name] = JsonValueConverter.ToObject(property.Value);
        }

        return new DataRecord { Fields = fields };
    }

    private static string BuildCompositeKey(DataRecord record, IReadOnlyList<string> keyFields)
    {
        var keyParts = new List<string>(keyFields.Count);
        foreach (var keyField in keyFields)
        {
            record.Fields.TryGetValue(keyField, out var value);
            keyParts.Add(value?.ToString() ?? string.Empty);
        }

        return string.Join("|", keyParts);
    }
}

public static class JsonValueConverter
{
    public static object? ToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var int64Value) => int64Value,
            JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray().Select(ToObject).ToList(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(
                property => property.Name,
                property => ToObject(property.Value),
                StringComparer.OrdinalIgnoreCase),
            _ => element.ToString()
        };
    }
}
