using Clothesline.Core.Models;
using Clothesline.Core.Storage;
using Xunit;

namespace Clothesline.Tests;

[Collection("StateStore")]
public class CoreTests
{
    [Theory]
    [InlineData("pdf", "PDF", "#E02424", ItemKind.Document)]
    [InlineData("docx", "DOCX", "#1A56DB", ItemKind.Document)]
    [InlineData("pptx", "PPTX", "#D97706", ItemKind.Presentation)]
    [InlineData("xlsx", "XLSX", "#0E9F6E", ItemKind.Spreadsheet)]
    [InlineData("png", "PNG", "#7E3AF2", ItemKind.Image)]
    [InlineData("webp", "WEBP", "#059669", ItemKind.Image)]
    [InlineData("mp4", "MP4", "#4B5563", ItemKind.Media)]
    [InlineData("txt", "TXT", "#1F2937", ItemKind.Text)]
    public void BadgePalette_ShouldResolveExpectedStyleAndKind(string ext, string expectedText, string expectedBg, ItemKind expectedKind)
    {
        var badge = BadgePalette.Resolve(ext);
        var kind = BadgePalette.ResolveKind(ext);

        Assert.Equal(expectedText, badge.Text);
        Assert.Equal(expectedBg, badge.BackgroundColorHex);
        Assert.Equal(expectedKind, kind);
    }

    [Fact]
    public async Task StateStore_SaveAndLoad_ShouldPreserveData()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "Clothesline_CoreTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "testfile.txt");
        await File.WriteAllTextAsync(tempFile, "sample data");
        try
        {
            var store = new StateStore(tempDir);
            var item = RopeItem.FromFile(tempFile, isTemporaryCopy: false);
            await store.AddItemAsync(item);

            var loaded = await store.LoadAsync();
            Assert.Contains(loaded, x => x.FilePath == tempFile);

            // 解挂测试：从绳上移除
            await store.RemoveItemAsync(item.Id);
            var afterRemove = await store.LoadAsync();
            Assert.DoesNotContain(afterRemove, x => x.Id == item.Id);

            // 铁律验证：磁盘物理文件绝对没有被删除！
            Assert.True(File.Exists(tempFile), "解挂绝对不能删除本地源文件！");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}
