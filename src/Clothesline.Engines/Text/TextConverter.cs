using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Clothesline.Engines.Abstractions;
using SkiaSharp;

namespace Clothesline.Engines.Text;

/// <summary>
/// 纯文本与 Markdown 排版互转引擎（MD ↔ HTML ↔ TXT，及高保真流式多页 PDF 排版）
/// </summary>
public class TextConverter : IConverter
{
    private static readonly HashSet<string> SupportedSource = new(StringComparer.OrdinalIgnoreCase)
    {
        "TXT", "MD", "LOG", "JSON", "XML", "CS", "JS", "PY"
    };

    public bool CanConvert(string sourceExt, string targetExt)
    {
        var src = sourceExt.TrimStart('.').ToUpperInvariant();
        var tgt = targetExt.TrimStart('.').ToUpperInvariant();

        if (src == "MD" && (tgt is "HTML" or "TXT" or "PDF")) return true;
        if (src == "TXT" && (tgt is "HTML" or "PDF")) return true;
        if (SupportedSource.Contains(src) && tgt == "PDF") return true;

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
            var srcExt = Path.GetExtension(sourceFilePath).TrimStart('.').ToLowerInvariant();
            var normalizedTargetExt = targetExt.TrimStart('.').ToLowerInvariant();
            var targetFileName = $"{fileNameWithoutExt}.{normalizedTargetExt}";
            var targetFilePath = Path.Combine(outputDirectory, targetFileName);

            if (File.Exists(targetFilePath))
            {
                targetFileName = $"{fileNameWithoutExt}_{DateTime.Now:HHmmss}.{normalizedTargetExt}";
                targetFilePath = Path.Combine(outputDirectory, targetFileName);
            }

            Directory.CreateDirectory(outputDirectory);

            if (normalizedTargetExt == "html")
            {
                ConvertMarkdownToHtml(sourceFilePath, targetFilePath);
                progress?.Report(1.0);
                return targetFilePath;
            }

            if (srcExt == "md" && normalizedTargetExt == "txt")
            {
                ConvertMarkdownToTxt(sourceFilePath, targetFilePath);
                progress?.Report(1.0);
                return targetFilePath;
            }

            // PDF 排版生成
            var lines = File.ReadAllLines(sourceFilePath);
            progress?.Report(0.3);

            float pageWidth = 595f;
            float pageHeight = 842f; // 标准 A4 纸张
            float margin = 48f;

            using var outputStream = File.Create(targetFilePath);
            using var document = SKDocument.CreatePdf(outputStream);

            using var cjkTypeface = Helpers.EngineFontHelper.GetCjkTypeface();
            using var font = new SKFont(cjkTypeface, 11);
            using var textPaint = new SKPaint { Color = new SKColor(0x1E, 0x29, 0x3B), IsAntialias = true };
            using var bgPaint = new SKPaint { Color = SKColors.White };

            SKCanvas? currentCanvas = document.BeginPage(pageWidth, pageHeight);
            currentCanvas.DrawRect(0, 0, pageWidth, pageHeight, bgPaint);

            float currentY = margin + 20f;
            float lineHeight = 18f;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (currentY + lineHeight > pageHeight - margin)
                {
                    document.EndPage();
                    currentCanvas = document.BeginPage(pageWidth, pageHeight);
                    currentCanvas.DrawRect(0, 0, pageWidth, pageHeight, bgPaint);
                    currentY = margin + 20f;
                }

                currentCanvas.DrawText(line, margin, currentY, font, textPaint);
                currentY += lineHeight;
            }

            document.EndPage();
            document.Close();

            progress?.Report(1.0);
            return targetFilePath;
        }, cancellationToken);
    }

    private static void ConvertMarkdownToHtml(string mdPath, string htmlPath)
    {
        var lines = File.ReadAllLines(mdPath);
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Document</title>");
        sb.AppendLine("<style>body{font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,sans-serif;max-width:800px;margin:40px auto;padding:0 20px;line-height:1.6;color:#1e293b;background:#f8fafc;}h1,h2,h3{color:#0f172a;border-bottom:1px solid #e2e8f0;padding-bottom:8px;}code{background:#e2e8f0;padding:2px 6px;border-radius:4px;font-size:0.9em;}pre{background:#1e293b;color:#f8fafc;padding:16px;border-radius:8px;overflow-x:auto;}</style>");
        sb.AppendLine("</head><body>");

        bool inCodeBlock = false;
        foreach (var line in lines)
        {
            if (line.StartsWith("```"))
            {
                inCodeBlock = !inCodeBlock;
                sb.AppendLine(inCodeBlock ? "<pre><code>" : "</code></pre>");
                continue;
            }

            if (inCodeBlock)
            {
                sb.AppendLine(WebUtility.HtmlEncode(line));
                continue;
            }

            if (line.StartsWith("# "))
                sb.AppendLine($"<h1>{WebUtility.HtmlEncode(line[2..])}</h1>");
            else if (line.StartsWith("## "))
                sb.AppendLine($"<h2>{WebUtility.HtmlEncode(line[3..])}</h2>");
            else if (line.StartsWith("### "))
                sb.AppendLine($"<h3>{WebUtility.HtmlEncode(line[4..])}</h3>");
            else if (line.StartsWith("- ") || line.StartsWith("* "))
                sb.AppendLine($"<li>{WebUtility.HtmlEncode(line[2..])}</li>");
            else if (string.IsNullOrWhiteSpace(line))
                sb.AppendLine("<br/>");
            else
                sb.AppendLine($"<p>{WebUtility.HtmlEncode(line)}</p>");
        }

        sb.AppendLine("</body></html>");
        File.WriteAllText(htmlPath, sb.ToString(), Encoding.UTF8);
    }

    private static void ConvertMarkdownToTxt(string mdPath, string txtPath)
    {
        var lines = File.ReadAllLines(mdPath);
        var cleanLines = lines.Select(l =>
        {
            var s = l.TrimStart();
            if (s.StartsWith("#")) s = s.TrimStart('#').TrimStart();
            if (s.StartsWith("- ") || s.StartsWith("* ")) s = "• " + s[2..];
            return s.Replace("**", "");
        });
        File.WriteAllLines(txtPath, cleanLines, Encoding.UTF8);
    }
}
