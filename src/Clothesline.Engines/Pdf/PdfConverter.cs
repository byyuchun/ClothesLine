using System.Text;
using Clothesline.Engines.Abstractions;
using UglyToad.PdfPig;

namespace Clothesline.Engines.Pdf;

/// <summary>
/// 基于 PdfPig 的 PDF 高保真文本提取与分析转换器
/// </summary>
public class PdfConverter : IConverter
{
    public bool CanConvert(string sourceExt, string targetExt)
    {
        var src = sourceExt.TrimStart('.').ToUpperInvariant();
        var tgt = targetExt.TrimStart('.').ToUpperInvariant();

        return src == "PDF" && (tgt is "TXT" or "MD" or "HTML");
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

            using var document = PdfDocument.Open(sourceFilePath);
            var sb = new StringBuilder();

            int totalPages = document.NumberOfPages;
            for (int i = 1; i <= totalPages; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var page = document.GetPage(i);
                sb.AppendLine($"--- 第 {i} 页 ---");
                sb.AppendLine(page.Text);
                sb.AppendLine();

                progress?.Report(0.1 + 0.8 * (i / (double)totalPages));
            }

            if (normalizedTargetExt == "html")
            {
                var htmlSb = new StringBuilder();
                htmlSb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'><title>PDF Export</title>");
                htmlSb.AppendLine("<style>body{font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,sans-serif;max-width:850px;margin:30px auto;padding:0 20px;color:#1e293b;background:#f8fafc;}.page-box{background:#fff;border:1px solid #e2e8f0;border-radius:8px;padding:30px;margin-bottom:24px;box-shadow:0 4px 12px rgba(0,0,0,0.05);white-space:pre-wrap;line-height:1.6;}.page-header{font-size:12px;font-weight:bold;color:#64748b;margin-bottom:12px;border-bottom:1px solid #f1f5f9;padding-bottom:6px;}</style>");
                htmlSb.AppendLine("</head><body>");

                for (int i = 1; i <= totalPages; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var page = document.GetPage(i);
                    htmlSb.AppendLine($"<div class='page-box'><div class='page-header'>第 {i} 页 / 共 {totalPages} 页</div>{System.Net.WebUtility.HtmlEncode(page.Text)}</div>");
                    progress?.Report(0.1 + 0.8 * (i / (double)totalPages));
                }

                htmlSb.AppendLine("</body></html>");
                File.WriteAllText(targetFilePath, htmlSb.ToString(), Encoding.UTF8);
            }
            else
            {
                File.WriteAllText(targetFilePath, sb.ToString(), Encoding.UTF8);
            }

            progress?.Report(1.0);
            return targetFilePath;
        }, cancellationToken);
    }
}
