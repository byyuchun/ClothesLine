using System.Text;
using Avalonia.Media.Imaging;
using Clothesline.Core.Models;

namespace Clothesline.App.Services;

public record PreviewResult(
    ItemKind Kind,
    string Title,
    string MetaInfo,
    Bitmap? Image = null,
    string? TextLines = null,
    string? AdditionalDetails = null
);

/// <summary>
/// 多模态卡片悬浮预览异步生成器
/// </summary>
public static class PreviewGenerator
{
    public static async Task<PreviewResult> GenerateAsync(RopeItem item)
    {
        if (!File.Exists(item.FilePath))
        {
            return new PreviewResult(
                ItemKind.Other,
                item.FileName,
                "文件不存在或已被移动"
            );
        }

        var fileInfo = new FileInfo(item.FilePath);
        string sizeStr = item.FormattedFileSize;
        string modTime = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm");

        // 1. 图片格式解析
        if (item.Kind == ItemKind.Image)
        {
            try
            {
                var (bitmap, pixelWidth, pixelHeight) = await Task.Run(() =>
                {
                    using var stream = File.OpenRead(item.FilePath);
                    // 解码为适合大图浮窗的高清位图 (最大宽 640px)
                    var bmp = Bitmap.DecodeToWidth(stream, 640);
                    return (bmp, bmp.PixelSize.Width, bmp.PixelSize.Height);
                });

                return new PreviewResult(
                    ItemKind.Image,
                    item.FileName,
                    $"{pixelWidth} × {pixelHeight} · {sizeStr}",
                    Image: bitmap,
                    AdditionalDetails: $"修改时间: {modTime}"
                );
            }
            catch
            {
                // 图片解码失败回退
            }
        }

        // 2. 纯文本 / Markdown / 代码格式解析
        if (item.Kind == ItemKind.Text || item.Extension is "CS" or "JSON" or "XML" or "YAML" or "YML" or "PY" or "JS" or "TS" or "HTML" or "CSS")
        {
            try
            {
                var (previewContent, lineCount) = await Task.Run(async () =>
                {
                    using var stream = File.OpenRead(item.FilePath);
                    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

                    var sb = new StringBuilder();
                    int count = 0;
                    while (await reader.ReadLineAsync() is { } line && count < 25)
                    {
                        count++;
                        sb.AppendLine($"{count,2} │ {line}");
                    }
                    return (sb.ToString(), count);
                });

                return new PreviewResult(
                    ItemKind.Text,
                    item.FileName,
                    $"{sizeStr} · 前 {lineCount} 行摘要",
                    TextLines: previewContent,
                    AdditionalDetails: $"路径: {item.FilePath}\n修改时间: {modTime}"
                );
            }
            catch
            {
                // 文本读取异常回退
            }
        }

        // 3. 办公文档 (PDF / Word / PPT / Excel)
        if (item.Kind is ItemKind.Document or ItemKind.Presentation or ItemKind.Spreadsheet)
        {
            string categoryName = item.Kind switch
            {
                ItemKind.Document => "文档",
                ItemKind.Presentation => "演示文稿",
                ItemKind.Spreadsheet => "电子表格",
                _ => "文件"
            };

            return new PreviewResult(
                item.Kind,
                item.FileName,
                $"{item.Extension} {categoryName} · {sizeStr}",
                AdditionalDetails: $"格式: {item.Extension}\n大小: {sizeStr} ({fileInfo.Length:N0} 字节)\n修改时间: {modTime}\n路径: {item.FilePath}"
            );
        }

        // 4. 影音多媒体与其他格式
        return new PreviewResult(
            item.Kind,
            item.FileName,
            $"{item.Extension} · {sizeStr}",
            AdditionalDetails: $"大小: {sizeStr}\n修改时间: {modTime}\n完整路径: {item.FilePath}"
        );
    }
}
