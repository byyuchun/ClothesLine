using System.Text;
using Clothesline.Engines;
using Clothesline.Engines.Image;
using Clothesline.Engines.Office;
using Clothesline.Engines.Text;
using SkiaSharp;
using Xunit;

namespace Clothesline.Tests;

public class ConversionTests
{
    [Theory]
    [InlineData("png", new[] { "JPG", "WEBP", "PDF", "ICO" })]
    [InlineData("pptx", new[] { "PDF" })]
    [InlineData("docx", new[] { "PDF", "TXT", "MD" })]
    [InlineData("xlsx", new[] { "CSV", "PDF" })]
    [InlineData("txt", new[] { "PDF", "HTML" })]
    [InlineData("md", new[] { "HTML", "TXT", "PDF" })]
    [InlineData("json", new[] { "XML", "CSV", "PDF" })]
    [InlineData("csv", new[] { "JSON", "TXT", "PDF" })]
    [InlineData("mp4", new[] { "MP3", "WAV" })]
    public void ConversionService_GetTargetFormats_ShouldReturnCuratedTargets(string ext, string[] expectedTargets)
    {
        var targets = ConversionService.Current.GetTargetFormats(ext);
        foreach (var expected in expectedTargets)
        {
            Assert.Contains(expected, targets);
        }
    }

    [Fact]
    public void ConversionService_GetTargetFormats_UnsupportedShouldReturnEmpty()
    {
        var targets = ConversionService.Current.GetTargetFormats("unknown_binary_xyz");
        Assert.Empty(targets);
    }

    [Fact]
    public void EngineFontHelper_ShouldResolveValidCjkTypefaceWithRealGlyphs()
    {
        var typeface = Clothesline.Engines.Helpers.EngineFontHelper.GetCjkTypeface();
        Assert.NotNull(typeface);
        ushort glyphId = typeface.GetGlyph('中');
        Assert.True(glyphId > 0, "CJK typeface must contain glyph for '中', avoiding tofu boxes");
    }

    [Fact]
    public async Task ImageConverter_ConvertPngToPdf_ShouldProduceValidPdf()
    {
        var converter = new ImageConverter();
        Assert.True(converter.CanConvert("PNG", "PDF"));

        var tempDir = Path.Combine(Path.GetTempPath(), "ClotheslineTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var sourcePng = Path.Combine(tempDir, "sample.png");

        try
        {
            using (var bmp = new SKBitmap(32, 32))
            {
                using var canvas = new SKCanvas(bmp);
                canvas.Clear(SKColors.CornflowerBlue);
                using var img = SKImage.FromBitmap(bmp);
                using var data = img.Encode(SKEncodedImageFormat.Png, 100);
                using var fs = File.Create(sourcePng);
                data.SaveTo(fs);
            }

            var outputPdf = await converter.ConvertAsync(sourcePng, "pdf", tempDir);

            Assert.True(File.Exists(outputPdf));
            Assert.EndsWith(".pdf", outputPdf, StringComparison.OrdinalIgnoreCase);

            // 铁律验证：文件头必须包含标准 PDF 标识 %PDF，绝非伪装复制
            var headerBytes = new byte[4];
            using (var stream = File.OpenRead(outputPdf))
            {
                stream.ReadExactly(headerBytes);
            }
            string header = Encoding.ASCII.GetString(headerBytes);
            Assert.Equal("%PDF", header);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task OfficeConverter_ConvertPptxToPdf_ShouldProduceValidPdf()
    {
        var converter = new OfficeConverter();
        Assert.True(converter.CanConvert("PPTX", "PDF"));

        var tempDir = Path.Combine(Path.GetTempPath(), "ClotheslineTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var sourcePptx = Path.Combine(tempDir, "presentation.pptx");

        try
        {
            // 构造最简演示文稿文件测试
            File.WriteAllText(sourcePptx, "Dummy presentation content");

            var outputPdf = await converter.ConvertAsync(sourcePptx, "pdf", tempDir);

            Assert.True(File.Exists(outputPdf));
            Assert.EndsWith(".pdf", outputPdf, StringComparison.OrdinalIgnoreCase);

            var headerBytes = new byte[4];
            using (var stream = File.OpenRead(outputPdf))
            {
                stream.ReadExactly(headerBytes);
            }
            string header = Encoding.ASCII.GetString(headerBytes);
            Assert.Equal("%PDF", header);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task TextConverter_ConvertTxtToPdf_ShouldProduceValidPdf()
    {
        var converter = new TextConverter();
        Assert.True(converter.CanConvert("TXT", "PDF"));

        var tempDir = Path.Combine(Path.GetTempPath(), "ClotheslineTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var sourceTxt = Path.Combine(tempDir, "notes.txt");

        try
        {
            File.WriteAllText(sourceTxt, "Hello Clothesline\nLine 2 Notes\nLine 3 Report");

            var outputPdf = await converter.ConvertAsync(sourceTxt, "pdf", tempDir);

            Assert.True(File.Exists(outputPdf));
            Assert.EndsWith(".pdf", outputPdf, StringComparison.OrdinalIgnoreCase);

            var headerBytes = new byte[4];
            using (var stream = File.OpenRead(outputPdf))
            {
                stream.ReadExactly(headerBytes);
            }
            string header = Encoding.ASCII.GetString(headerBytes);
            Assert.Equal("%PDF", header);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task DataConverter_ConvertJsonToXmlAndCsv_ShouldProduceValidOutput()
    {
        var converter = new Clothesline.Engines.Data.DataConverter();
        Assert.True(converter.CanConvert("JSON", "XML"));
        Assert.True(converter.CanConvert("JSON", "CSV"));
        Assert.True(converter.CanConvert("CSV", "JSON"));

        var tempDir = Path.Combine(Path.GetTempPath(), "ClotheslineDataTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceJson = Path.Combine(tempDir, "items.json");
            File.WriteAllText(sourceJson, "[{\"id\":\"1\",\"name\":\"Alice\"},{\"id\":\"2\",\"name\":\"Bob\"}]");

            // JSON -> XML
            var outputXml = await converter.ConvertAsync(sourceJson, "xml", tempDir);
            Assert.True(File.Exists(outputXml));
            string xmlContent = File.ReadAllText(outputXml);
            Assert.Contains("<root>", xmlContent);
            Assert.Contains("Alice", xmlContent);

            // JSON -> CSV
            var outputCsv = await converter.ConvertAsync(sourceJson, "csv", tempDir);
            Assert.True(File.Exists(outputCsv));
            string csvContent = File.ReadAllText(outputCsv);
            Assert.Contains("id,name", csvContent);
            Assert.Contains("Alice", csvContent);

            // CSV -> JSON
            var outputJson = await converter.ConvertAsync(outputCsv, "json", tempDir);
            Assert.True(File.Exists(outputJson));
            string jsonContent = File.ReadAllText(outputJson);
            Assert.Contains("Bob", jsonContent);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task TextConverter_ConvertMdToHtml_ShouldProduceValidHtml()
    {
        var converter = new TextConverter();
        Assert.True(converter.CanConvert("MD", "HTML"));

        var tempDir = Path.Combine(Path.GetTempPath(), "ClotheslineMdTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var sourceMd = Path.Combine(tempDir, "readme.md");
            File.WriteAllText(sourceMd, "# Title\n\n- Point 1\n- Point 2\n\n```csharp\nint x = 1;\n```");

            var outputHtml = await converter.ConvertAsync(sourceMd, "html", tempDir);
            Assert.True(File.Exists(outputHtml));
            string htmlContent = File.ReadAllText(outputHtml);
            Assert.Contains("<h1>Title</h1>", htmlContent);
            Assert.Contains("<li>Point 1</li>", htmlContent);
            Assert.Contains("<pre><code>", htmlContent);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
