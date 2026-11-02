using System.Text.Json.Serialization;

namespace Clothesline.Core.Models;

/// <summary>
/// 挂在晾衣绳上的核心文件条目实体
/// </summary>
public class RopeItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public required string FilePath { get; set; }

    public required string FileName { get; set; }

    public string Extension => Path.GetExtension(FilePath).TrimStart('.').ToUpperInvariant();

    public ItemKind Kind { get; set; }

    public long FileSizeBytes { get; set; }

    /// <summary>
    /// 是否由格式转换器生成的衍生临时副本
    /// </summary>
    public bool IsTemporaryCopy { get; set; }

    public DateTime AddedTime { get; set; } = DateTime.Now;

    public DateTime LastAccessedTime { get; set; } = DateTime.Now;

    [JsonIgnore]
    public BadgeStyle Badge => BadgePalette.Resolve(Extension);

    [JsonIgnore]
    public string FormattedFileSize => FormatFileSize(FileSizeBytes);

    public static RopeItem FromFile(string filePath, bool isTemporaryCopy = false)
    {
        var fileInfo = new FileInfo(filePath);
        var ext = fileInfo.Extension.TrimStart('.').ToUpperInvariant();
        var size = fileInfo.Exists ? fileInfo.Length : 0;

        return new RopeItem
        {
            FilePath = fileInfo.FullName,
            FileName = fileInfo.Name,
            Kind = BadgePalette.ResolveKind(ext),
            FileSizeBytes = size,
            IsTemporaryCopy = isTemporaryCopy,
            AddedTime = DateTime.Now,
            LastAccessedTime = DateTime.Now
        };
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int i = 0;
        double d = bytes;
        while (d >= 1024 && i < suffixes.Length - 1)
        {
            d /= 1024;
            i++;
        }
        return $"{d:0.##} {suffixes[i]}";
    }
}
