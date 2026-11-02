using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Clothesline.Engines.Abstractions;

namespace Clothesline.Engines.Data;

/// <summary>
/// 高性能结构化数据转换引擎（JSON ↔ XML ↔ CSV ↔ TXT，基于纯净原生 .NET）
/// </summary>
public class DataConverter : IConverter
{
    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        "JSON", "XML", "CSV"
    };

    public bool CanConvert(string sourceExt, string targetExt)
    {
        var src = sourceExt.TrimStart('.').ToUpperInvariant();
        var tgt = targetExt.TrimStart('.').ToUpperInvariant();

        if (src == "JSON" && (tgt is "XML" or "CSV")) return true;
        if (src == "XML" && tgt is "JSON") return true;
        if (src == "CSV" && (tgt is "JSON" or "TXT")) return true;

        return false;
    }

    public async Task<string> ConvertAsync(
        string sourceFilePath,
        string targetExt,
        string outputDirectory,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(0.1);

        return await Task.Run(() =>
        {
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(sourceFilePath);
            var normalizedTargetExt = targetExt.TrimStart('.').ToLowerInvariant();
            var targetFileName = $"{fileNameWithoutExt}.{normalizedTargetExt}";
            var targetFilePath = Path.Combine(outputDirectory, targetFileName);

            if (File.Exists(targetFilePath))
            {
                targetFileName = $"{fileNameWithoutExt}_{DateTime.Now:HHmmss}.{normalizedTargetExt}";
                targetFilePath = Path.Combine(outputDirectory, targetFileName);
            }

            Directory.CreateDirectory(outputDirectory);

            var srcExt = Path.GetExtension(sourceFilePath).TrimStart('.').ToLowerInvariant();
            progress?.Report(0.3);

            if (srcExt == "json" && normalizedTargetExt == "xml")
            {
                ConvertJsonToXml(sourceFilePath, targetFilePath);
            }
            else if (srcExt == "json" && normalizedTargetExt == "csv")
            {
                ConvertJsonToCsv(sourceFilePath, targetFilePath);
            }
            else if (srcExt == "xml" && normalizedTargetExt == "json")
            {
                ConvertXmlToJson(sourceFilePath, targetFilePath);
            }
            else if (srcExt == "csv" && normalizedTargetExt == "json")
            {
                ConvertCsvToJson(sourceFilePath, targetFilePath);
            }
            else if (srcExt == "csv" && normalizedTargetExt == "txt")
            {
                ConvertCsvToTxtTable(sourceFilePath, targetFilePath);
            }
            else
            {
                throw new NotSupportedException($"不支持的数据转换: {srcExt} -> {normalizedTargetExt}");
            }

            progress?.Report(1.0);
            return targetFilePath;
        }, cancellationToken);
    }

    private static void ConvertJsonToXml(string jsonPath, string xmlPath)
    {
        var jsonText = File.ReadAllText(jsonPath);
        using var jsonDoc = JsonDocument.Parse(jsonText);

        var root = new XElement("root");
        BuildXmlElement(jsonDoc.RootElement, root);

        var xdoc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
        xdoc.Save(xmlPath);
    }

    private static void BuildXmlElement(JsonElement element, XElement parent)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    string safeName = SanitizeXmlName(prop.Name);
                    var child = new XElement(safeName);
                    BuildXmlElement(prop.Value, child);
                    parent.Add(child);
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var itemElem = new XElement("item");
                    BuildXmlElement(item, itemElem);
                    parent.Add(itemElem);
                }
                break;

            default:
                parent.Value = element.ToString();
                break;
        }
    }

    private static string SanitizeXmlName(string name)
    {
        var sb = new StringBuilder();
        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                sb.Append(c);
            else
                sb.Append('_');
        }
        string result = sb.ToString();
        if (string.IsNullOrEmpty(result) || char.IsDigit(result[0]))
            result = "_" + result;
        return result;
    }

    private static void ConvertXmlToJson(string xmlPath, string jsonPath)
    {
        var xdoc = XDocument.Load(xmlPath);
        var options = new JsonWriterOptions { Indented = true };

        using var fs = File.Create(jsonPath);
        using var writer = new Utf8JsonWriter(fs, options);

        writer.WriteStartObject();
        if (xdoc.Root != null)
        {
            writer.WritePropertyName(xdoc.Root.Name.LocalName);
            WriteXmlElementToJson(xdoc.Root, writer);
        }
        writer.WriteEndObject();
    }

    private static void WriteXmlElementToJson(XElement element, Utf8JsonWriter writer)
    {
        var children = element.Elements().ToList();
        if (children.Count == 0)
        {
            writer.WriteStringValue(element.Value);
            return;
        }

        writer.WriteStartObject();
        var groups = children.GroupBy(c => c.Name.LocalName);
        foreach (var group in groups)
        {
            var list = group.ToList();
            if (list.Count > 1)
            {
                writer.WriteStartArray(group.Key);
                foreach (var item in list)
                {
                    WriteXmlElementToJson(item, writer);
                }
                writer.WriteEndArray();
            }
            else
            {
                writer.WritePropertyName(group.Key);
                WriteXmlElementToJson(list[0], writer);
            }
        }
        writer.WriteEndObject();
    }

    private static void ConvertJsonToCsv(string jsonPath, string csvPath)
    {
        var jsonText = File.ReadAllText(jsonPath);
        using var doc = JsonDocument.Parse(jsonText);

        var sb = new StringBuilder();

        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            var items = doc.RootElement.EnumerateArray().ToList();
            if (items.Count > 0 && items[0].ValueKind == JsonValueKind.Object)
            {
                var headers = items[0].EnumerateObject().Select(p => p.Name).ToList();
                sb.AppendLine(string.Join(",", headers.Select(EscapeCsvCell)));

                foreach (var item in items)
                {
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        var row = headers.Select(h =>
                            item.TryGetProperty(h, out var prop) ? EscapeCsvCell(prop.ToString()) : "");
                        sb.AppendLine(string.Join(",", row));
                    }
                }
            }
        }
        else if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            sb.AppendLine("Key,Value");
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                sb.AppendLine($"{EscapeCsvCell(prop.Name)},{EscapeCsvCell(prop.Value.ToString())}");
            }
        }

        File.WriteAllText(csvPath, sb.ToString(), Encoding.UTF8);
    }

    private static void ConvertCsvToJson(string csvPath, string jsonPath)
    {
        var lines = File.ReadAllLines(csvPath).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        if (lines.Count == 0)
        {
            File.WriteAllText(jsonPath, "[]", Encoding.UTF8);
            return;
        }

        var headers = ParseCsvLine(lines[0]);
        var options = new JsonWriterOptions { Indented = true };

        using var fs = File.Create(jsonPath);
        using var writer = new Utf8JsonWriter(fs, options);

        writer.WriteStartArray();
        for (int i = 1; i < lines.Count; i++)
        {
            var cells = ParseCsvLine(lines[i]);
            writer.WriteStartObject();
            for (int h = 0; h < headers.Count; h++)
            {
                string headerName = headers[h];
                string cellValue = h < cells.Count ? cells[h] : "";
                writer.WriteString(headerName, cellValue);
            }
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void ConvertCsvToTxtTable(string csvPath, string txtPath)
    {
        var lines = File.ReadAllLines(csvPath).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        if (lines.Count == 0)
        {
            File.WriteAllText(txtPath, "(空数据表)", Encoding.UTF8);
            return;
        }

        var table = lines.Select(ParseCsvLine).ToList();
        int colCount = table.Max(r => r.Count);

        // 计算每列最大宽度
        int[] colWidths = new int[colCount];
        for (int c = 0; c < colCount; c++)
        {
            int maxW = 4;
            foreach (var row in table)
            {
                if (c < row.Count)
                {
                    int w = Encoding.UTF8.GetByteCount(row[c]);
                    if (w > maxW) maxW = Math.Min(w, 40);
                }
            }
            colWidths[c] = maxW;
        }

        var sb = new StringBuilder();
        string divider = "+-" + string.Join("-+-", colWidths.Select(w => new string('-', w))) + "-+";

        sb.AppendLine(divider);
        for (int r = 0; r < table.Count; r++)
        {
            var row = table[r];
            var cells = new List<string>();
            for (int c = 0; c < colCount; c++)
            {
                string text = c < row.Count ? row[c] : "";
                cells.Add(PadRightUnicode(text, colWidths[c]));
            }
            sb.AppendLine("| " + string.Join(" | ", cells) + " |");
            if (r == 0) sb.AppendLine(divider); // 表头下划线
        }
        sb.AppendLine(divider);

        File.WriteAllText(txtPath, sb.ToString(), Encoding.UTF8);
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString().Trim());
        return result;
    }

    private static string EscapeCsvCell(string text)
    {
        if (text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r'))
        {
            return $"\"{text.Replace("\"", "\"\"")}\"";
        }
        return text;
    }

    private static string PadRightUnicode(string text, int targetWidth)
    {
        int currentBytes = Encoding.UTF8.GetByteCount(text);
        if (currentBytes >= targetWidth) return text;
        return text + new string(' ', targetWidth - currentBytes);
    }
}
