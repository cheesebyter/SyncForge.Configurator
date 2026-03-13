using System.Globalization;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using SyncForge.Abstractions.Configuration;

namespace SyncForge.Configurator.Services;

public static class SourcePreviewService
{
    public static async Task<IReadOnlyList<string>> LoadColumnsAsync(JobDefinition definition, string? currentFilePath)
    {
        var sourceType = Normalize(definition.Source.Type);
        if (string.IsNullOrWhiteSpace(sourceType))
        {
            sourceType = InferSourceType(definition.Source.Settings);
        }

        if (sourceType == "csv")
        {
            return await ReadCsvHeaderAsync(definition, currentFilePath);
        }

        if (sourceType == "xlsx")
        {
            return ReadXlsxHeader(definition, currentFilePath);
        }

        if (sourceType == "jsonl")
        {
            return await ReadJsonlHeaderAsync(definition, currentFilePath);
        }

        throw new InvalidOperationException($"Source preview for type '{definition.Source.Type}' is not available in UI-6.");
    }

    private static string InferSourceType(IReadOnlyDictionary<string, string?> settings)
    {
        if (settings.ContainsKey("delimiter"))
        {
            return "csv";
        }

        if (settings.TryGetValue("path", out var path) && !string.IsNullOrWhiteSpace(path))
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension == ".csv")
            {
                return "csv";
            }

            if (extension == ".xlsx")
            {
                return "xlsx";
            }

            if (extension == ".jsonl")
            {
                return "jsonl";
            }
        }

        if (settings.ContainsKey("url"))
        {
            return "rest";
        }

        return string.Empty;
    }

    private static async Task<IReadOnlyList<string>> ReadCsvHeaderAsync(JobDefinition definition, string? currentFilePath)
    {
        var path = ResolvePath(definition.Source.Settings, currentFilePath);
        var settings = CsvSourceSettings.From(new Dictionary<string, string?>(definition.Source.Settings, StringComparer.OrdinalIgnoreCase)
        {
            ["path"] = path
        });

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(stream, settings.GetEncoding(), detectEncodingFromByteOrderMarks: true);

        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = settings.Delimiter,
            HasHeaderRecord = settings.HasHeader,
            Quote = settings.Quote,
            Escape = settings.Escape,
            BadDataFound = null,
            MissingFieldFound = null,
            TrimOptions = TrimOptions.Trim
        };

        using var csv = new CsvReader(reader, csvConfig);
        if (!await csv.ReadAsync())
        {
            throw new InvalidOperationException("CSV file is empty.");
        }

        if (settings.HasHeader)
        {
            csv.ReadHeader();
            return CsvSourceSettings.ValidateHeaderColumns(csv.HeaderRecord ?? []);
        }

        var firstRecord = csv.Parser.Record ?? [];
        return CsvSourceSettings.BuildGeneratedHeaders(firstRecord.Length);
    }

    private static IReadOnlyList<string> ReadXlsxHeader(JobDefinition definition, string? currentFilePath)
    {
        var path = ResolvePath(definition.Source.Settings, currentFilePath);
        var sheetName = definition.Source.Settings.TryGetValue("sheetName", out var name) ? name : null;

        using var document = SpreadsheetDocument.Open(path, false);
        var workbookPart = document.WorkbookPart ?? throw new InvalidOperationException("WorkbookPart missing in XLSX file.");
        var sheet = ResolveSheet(workbookPart, sheetName);
        if (sheet.Id is null)
        {
            throw new InvalidOperationException("Sheet relationship id is missing.");
        }

        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id.Value!);
        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;
        var styleTable = workbookPart.WorkbookStylesPart?.Stylesheet;

        using var oxReader = OpenXmlReader.Create(worksheetPart);
        while (oxReader.Read())
        {
            if (oxReader.ElementType != typeof(Row) || !oxReader.IsStartElement)
            {
                continue;
            }

            var row = oxReader.LoadCurrentElement() as Row;
            if (row is null)
            {
                continue;
            }

            return row.Elements<Cell>()
                .Select(cell => ReadCellValue(cell, sharedStrings, styleTable)?.ToString()?.Trim() ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        throw new InvalidOperationException("XLSX header row is missing.");
    }

    private static async Task<IReadOnlyList<string>> ReadJsonlHeaderAsync(JobDefinition definition, string? currentFilePath)
    {
        var path = ResolvePath(definition.Source.Settings, currentFilePath);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var doc = JsonDocument.Parse(line);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("JSONL preview expects object rows.");
            }

            return doc.RootElement.EnumerateObject()
                .Select(prop => prop.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        throw new InvalidOperationException("JSONL source file has no data rows.");
    }

    private static string ResolvePath(IReadOnlyDictionary<string, string?> settings, string? currentFilePath)
    {
        if (!settings.TryGetValue("path", out var rawPath) || string.IsNullOrWhiteSpace(rawPath))
        {
            throw new InvalidOperationException("Source setting 'path' is required for preview.");
        }

        if (Path.IsPathRooted(rawPath))
        {
            return rawPath;
        }

        var baseDir = string.IsNullOrWhiteSpace(currentFilePath)
            ? Directory.GetCurrentDirectory()
            : Path.GetDirectoryName(Path.GetFullPath(currentFilePath)) ?? Directory.GetCurrentDirectory();

        return Path.GetFullPath(Path.Combine(baseDir, rawPath));
    }

    private static Sheet ResolveSheet(WorkbookPart workbookPart, string? configuredSheet)
    {
        var sheets = workbookPart.Workbook.Sheets?.Elements<Sheet>().ToList()
            ?? throw new InvalidOperationException("Workbook has no sheets.");

        if (sheets.Count == 0)
        {
            throw new InvalidOperationException("Workbook has no sheets.");
        }

        if (string.IsNullOrWhiteSpace(configuredSheet))
        {
            return sheets[0];
        }

        var match = sheets.FirstOrDefault(sheet =>
            string.Equals(sheet.Name?.Value, configuredSheet, StringComparison.OrdinalIgnoreCase));

        return match ?? throw new InvalidOperationException($"Configured sheet '{configuredSheet}' was not found.");
    }

    private static object? ReadCellValue(Cell cell, SharedStringTable? sharedStrings, Stylesheet? styles)
    {
        if (cell.CellValue is null && cell.InlineString is null)
        {
            return null;
        }

        var raw = cell.CellValue?.InnerText ?? cell.InlineString?.InnerText;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (cell.DataType is null)
        {
            if (IsDateStyle(cell, styles) && double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var oaDate))
            {
                return DateTime.FromOADate(oaDate).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var number))
            {
                return number;
            }

            return raw;
        }

        var dataType = cell.DataType.Value;
        if (dataType == CellValues.SharedString)
        {
            return ReadSharedString(raw, sharedStrings);
        }

        if (dataType == CellValues.Boolean)
        {
            return raw == "1";
        }

        return raw;
    }

    private static string? ReadSharedString(string raw, SharedStringTable? sharedStrings)
    {
        if (sharedStrings is null || !int.TryParse(raw, out var index))
        {
            return raw;
        }

        var item = sharedStrings.Elements<SharedStringItem>().ElementAtOrDefault(index);
        return item?.InnerText ?? raw;
    }

    private static bool IsDateStyle(Cell cell, Stylesheet? stylesheet)
    {
        if (stylesheet?.CellFormats is null || cell.StyleIndex is null)
        {
            return false;
        }

        var idx = (int)cell.StyleIndex.Value;
        var format = stylesheet.CellFormats.ChildElements.ElementAtOrDefault(idx) as CellFormat;
        if (format?.NumberFormatId is null)
        {
            return false;
        }

        var n = format.NumberFormatId.Value;
        return n is 14 or 15 or 16 or 17 or 22 or 45 or 46 or 47;
    }

    private static string Normalize(string value)
    {
        var chars = value.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars).ToLowerInvariant();
    }
}
