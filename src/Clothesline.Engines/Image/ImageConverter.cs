using Clothesline.Engines.Abstractions;
using SkiaSharp;

namespace Clothesline.Engines.Image;

/// <summary>
/// 基于 SkiaSharp 的高性能图片互转引擎（PNG / JPG / WEBP / ICO / BMP / PDF）
/// </summary>
public class ImageConverter : IConverter
{
    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        "PNG", "JPG", "JPEG", "WEBP", "BMP", "ICO"
    };

    public bool CanConvert(string sourceExt, string targetExt)
    {
        bool isSourceSupported = Supported.Contains(sourceExt);
        bool isTargetSupported = Supported.Contains(targetExt) || string.Equals(targetExt, "PDF", StringComparison.OrdinalIgnoreCase);
        return isSourceSupported && isTargetSupported;
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

            // 如果已存在同名衍生临时文件，附加唯一标识
            if (File.Exists(targetFilePath))
            {
                targetFileName = $"{fileNameWithoutExt}_{DateTime.Now:HHmmss}.{normalizedTargetExt}";
                targetFilePath = Path.Combine(outputDirectory, targetFileName);
            }

            Directory.CreateDirectory(outputDirectory);

            using var inputStream = File.OpenRead(sourceFilePath);
            using var codec = SKCodec.Create(inputStream);
            if (codec == null)
            {
                throw new InvalidOperationException($"无法解码图像: {sourceFilePath}");
            }

            using var bitmap = SKBitmap.Decode(codec);
            progress?.Report(0.5);

            // 特殊分支：图片生成真矢量/光栅合规 PDF
            if (normalizedTargetExt == "pdf")
            {
                using var outputStream = File.Create(targetFilePath);
                using var document = SKDocument.CreatePdf(outputStream);
                using var canvas = document.BeginPage(bitmap.Width, bitmap.Height);
                canvas.DrawBitmap(bitmap, 0, 0);
                document.EndPage();
                document.Close();

                progress?.Report(1.0);
                return targetFilePath;
            }

            var format = normalizedTargetExt switch
            {
                "png" => SKEncodedImageFormat.Png,
                "jpg" or "jpeg" => SKEncodedImageFormat.Jpeg,
                "webp" => SKEncodedImageFormat.Webp,
                "bmp" => SKEncodedImageFormat.Bmp,
                "ico" => SKEncodedImageFormat.Ico,
                _ => SKEncodedImageFormat.Png
            };

            int quality = normalizedTargetExt is "jpg" or "jpeg" ? 90 : 100;

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(format, quality);

            using var fileStream = File.Create(targetFilePath);
            data.SaveTo(fileStream);

            progress?.Report(1.0);
            return targetFilePath;
        }, cancellationToken);
    }
}
