namespace Clothesline.Core.Models;

/// <summary>
/// 格式语义调色盘：根据文件后缀智能解析徽章样式与文件类型归类
/// </summary>
public static class BadgePalette
{
    private static readonly Dictionary<string, (BadgeStyle Badge, ItemKind Kind)> Registry =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // PDF 文档
            ["PDF"] = (new BadgeStyle("PDF", "#E02424"), ItemKind.Document),

            // Word 文档
            ["DOCX"] = (new BadgeStyle("DOCX", "#1A56DB"), ItemKind.Document),
            ["DOC"] = (new BadgeStyle("DOC", "#1A56DB"), ItemKind.Document),

            // PowerPoint 幻灯片
            ["PPTX"] = (new BadgeStyle("PPTX", "#D97706"), ItemKind.Presentation),
            ["PPT"] = (new BadgeStyle("PPT", "#D97706"), ItemKind.Presentation),

            // Excel 电子表格
            ["XLSX"] = (new BadgeStyle("XLSX", "#0E9F6E"), ItemKind.Spreadsheet),
            ["XLS"] = (new BadgeStyle("XLS", "#0E9F6E"), ItemKind.Spreadsheet),
            ["CSV"] = (new BadgeStyle("CSV", "#059669"), ItemKind.Spreadsheet),

            // 图像类
            ["PNG"] = (new BadgeStyle("PNG", "#7E3AF2"), ItemKind.Image),
            ["JPG"] = (new BadgeStyle("JPG", "#7E3AF2"), ItemKind.Image),
            ["JPEG"] = (new BadgeStyle("JPEG", "#7E3AF2"), ItemKind.Image),
            ["WEBP"] = (new BadgeStyle("WEBP", "#059669"), ItemKind.Image),
            ["HEIC"] = (new BadgeStyle("HEIC", "#9333EA"), ItemKind.Image),
            ["HEIF"] = (new BadgeStyle("HEIF", "#9333EA"), ItemKind.Image),
            ["ICO"] = (new BadgeStyle("ICO", "#0284C7"), ItemKind.Image),
            ["GIF"] = (new BadgeStyle("GIF", "#EA580C"), ItemKind.Image),
            ["BMP"] = (new BadgeStyle("BMP", "#6366F1"), ItemKind.Image),
            ["SVG"] = (new BadgeStyle("SVG", "#F59E0B"), ItemKind.Image),

            // 影音多媒体
            ["MP4"] = (new BadgeStyle("MP4", "#4B5563"), ItemKind.Media),
            ["MOV"] = (new BadgeStyle("MOV", "#4B5563"), ItemKind.Media),
            ["MKV"] = (new BadgeStyle("MKV", "#4B5563"), ItemKind.Media),
            ["MP3"] = (new BadgeStyle("MP3", "#374151"), ItemKind.Media),
            ["WAV"] = (new BadgeStyle("WAV", "#374151"), ItemKind.Media),
            ["M4A"] = (new BadgeStyle("M4A", "#374151"), ItemKind.Media),
            ["FLAC"] = (new BadgeStyle("FLAC", "#374151"), ItemKind.Media),

            // 纯文本与标记
            ["MD"] = (new BadgeStyle("MD", "#1F2937"), ItemKind.Text),
            ["TXT"] = (new BadgeStyle("TXT", "#1F2937"), ItemKind.Text),
            ["JSON"] = (new BadgeStyle("JSON", "#0284C7"), ItemKind.Text),
            ["XML"] = (new BadgeStyle("XML", "#0284C7"), ItemKind.Text),
            ["LOG"] = (new BadgeStyle("LOG", "#4B5563"), ItemKind.Text),

            // 压缩包
            ["ZIP"] = (new BadgeStyle("ZIP", "#CA8A04"), ItemKind.Archive),
            ["RAR"] = (new BadgeStyle("RAR", "#CA8A04"), ItemKind.Archive),
            ["7Z"] = (new BadgeStyle("7Z", "#CA8A04"), ItemKind.Archive),
            ["TAR"] = (new BadgeStyle("TAR", "#CA8A04"), ItemKind.Archive),
            ["GZ"] = (new BadgeStyle("GZ", "#CA8A04"), ItemKind.Archive),
        };

    public static BadgeStyle Resolve(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return new BadgeStyle("FILE", "#6B7280");
        }

        var ext = extension.TrimStart('.').ToUpperInvariant();
        if (Registry.TryGetValue(ext, out var entry))
        {
            return entry.Badge;
        }

        // 默认徽标样式：截取前4位后缀名，使用优雅中性灰底色
        var label = ext.Length > 4 ? ext[..4] : ext;
        return new BadgeStyle(label, "#6B7280");
    }

    public static ItemKind ResolveKind(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return ItemKind.Other;
        }

        var ext = extension.TrimStart('.').ToUpperInvariant();
        if (Registry.TryGetValue(ext, out var entry))
        {
            return entry.Kind;
        }

        return ItemKind.Other;
    }
}
