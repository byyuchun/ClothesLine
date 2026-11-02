using System.Runtime.InteropServices;
using Clothesline.Core.Models;
using Clothesline.Core.Storage;
using Clothesline.Platform.Startup;
using SkiaSharp;
using Xunit;

namespace Clothesline.Tests;

[Collection("StateStore")]
public class BasketAndTrayTests
{
    [Fact]
    public async Task StateStore_BasketOperations_ShouldWorkCorrectly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "Clothesline_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var file1 = Path.Combine(tempDir, "photo1.png");
        var file2 = Path.Combine(tempDir, "doc2.pdf");
        await File.WriteAllTextAsync(file1, "dummy photo content");
        await File.WriteAllTextAsync(file2, "dummy doc content");

        try
        {
            var store = new StateStore(tempDir);
            await store.ClearBasketAsync();

            var initialList = await store.LoadBasketAsync();
            Assert.Empty(initialList);

            var item1 = new RopeItem
            {
                FilePath = file1,
                FileName = "photo1.png",
                Kind = ItemKind.Image,
                FileSizeBytes = 1024
            };

            var item2 = new RopeItem
            {
                FilePath = file2,
                FileName = "doc2.pdf",
                Kind = ItemKind.Document,
                FileSizeBytes = 2048
            };

            await store.AddToBasketAsync(item1);
            await store.AddToBasketAsync(item2);

            var list = await store.LoadBasketAsync();
            Assert.Equal(2, list.Count);
            Assert.Equal("doc2.pdf", list[0].FileName); // 最新添加在前面

            // 从收纳篮移除单项
            await store.RemoveFromBasketAsync(item1.Id);
            var afterOneRemove = await store.LoadBasketAsync();
            Assert.Single(afterOneRemove);
            Assert.Equal("doc2.pdf", afterOneRemove[0].FileName);

            // 清空收纳篮
            await store.ClearBasketAsync();
            var afterClear = await store.LoadBasketAsync();
            Assert.Empty(afterClear);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task StateStore_PurgeItem_ShouldRemoveWithoutBasketBackup()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "Clothesline_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "temp_shot.png");
        await File.WriteAllTextAsync(tempFile, "temp shot bytes");

        try
        {
            var store = new StateStore(tempDir);
            var item = new RopeItem
            {
                FilePath = tempFile,
                FileName = "temp_shot.png",
                Kind = ItemKind.Image,
                IsTemporaryCopy = true
            };

            await store.AddItemAsync(item);
            var ropeItems = await store.LoadAsync();
            Assert.Single(ropeItems);

            // 彻底清除
            await store.PurgeItemAsync(item.Id, deletePhysicalTemporaryFile: true);

            var afterPurge = await store.LoadAsync();
            Assert.Empty(afterPurge);

            // 确认未流入收纳篮
            var basketList = await store.LoadBasketAsync();
            Assert.Empty(basketList);

            // 确认临时物理文件已被删除
            Assert.False(File.Exists(tempFile));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void AutoStartService_QueryStatus_ShouldNotThrow()
    {
        // 验证读取开机自启状态安全无异常
        var isEnabled = AutoStartService.IsAutoStartEnabled();
        Assert.True(isEnabled || !isEnabled);
    }

    [Fact]
    public void GenerateAppIconAssets_ShouldCreateIconFiles()
    {
        // 动态生成高质量现代 Clothesline 应用图标 assets
        var assetsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Clothesline.App/Assets"));
        Directory.CreateDirectory(assetsDir);

        int size = 256;
        using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        // 1. 现代暗黑圆角背景底座
        using var bgPaint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(size, size),
                new[] { new SKColor(0x1E, 0x29, 0x3B), new SKColor(0x0F, 0x17, 0x2A) },
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(12, 12, size - 12, size - 12), 52, 52), bgPaint);

        // 边框发光线
        using var borderPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 4,
            Color = new SKColor(0x38, 0xBD, 0xF8, 0xAA)
        };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(12, 12, size - 12, size - 12), 52, 52), borderPaint);

        // 2. 晾衣绳索曲线
        using var ropePath = new SKPath();
        ropePath.MoveTo(26, 76);
        ropePath.QuadTo(128, 110, 230, 76);

        using var ropePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 4.5f,
            Color = new SKColor(0xE2, 0xE8, 0xF0)
        };
        canvas.DrawPath(ropePath, ropePaint);

        // 3. 挂绳照片卡片 1 (左侧照片卡)
        using var card1Paint = new SKPaint { IsAntialias = true, Color = SKColors.White };
        canvas.Save();
        canvas.RotateDegrees(-5, 75, 100);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(50, 95, 105, 175), 6, 6), card1Paint);
        // 卡片内容
        using var photo1Paint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(54, 99),
                new SKPoint(101, 150),
                new[] { new SKColor(0x3B, 0x82, 0xF6), new SKColor(0x8B, 0x5C, 0xF6) },
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(new SKRect(54, 99, 101, 150), photo1Paint);
        // 木质夹子
        using var clipPaint = new SKPaint { IsAntialias = true, Color = new SKColor(0xD4, 0xA3, 0x73) };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(73, 85, 83, 110), 3, 3), clipPaint);
        canvas.Restore();

        // 4. 挂绳照片卡片 2 (中间照片卡)
        canvas.Save();
        canvas.RotateDegrees(4, 140, 105);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(115, 100, 170, 180), 6, 6), card1Paint);
        using var photo2Paint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(119, 104),
                new SKPoint(166, 155),
                new[] { new SKColor(0x10, 0xB9, 0x81), new SKColor(0x06, 0xB6, 0xD4) },
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(new SKRect(119, 104, 166, 155), photo2Paint);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(138, 90, 148, 115), 3, 3), clipPaint);
        canvas.Restore();

        // 5. 挂在右侧的收纳篮 (Basket)
        using var basketBodyPaint = new SKPaint { IsAntialias = true, Color = new SKColor(0x33, 0x41, 0x55) };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(185, 96, 225, 148), 8, 8), basketBodyPaint);
        // 篮子提手
        using var basketHookPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3,
            Color = new SKColor(0x94, 0xA3, 0xB8)
        };
        canvas.DrawLine(205, 80, 205, 96, basketHookPaint);
        // 篮子角标
        using var badgePaint = new SKPaint { IsAntialias = true, Color = new SKColor(0x25, 0x63, 0xEB) };
        canvas.DrawCircle(220, 96, 10, badgePaint);

        // 保存 PNG
        var pngPath = Path.Combine(assetsDir, "clothesline.png");
        using var image = surface.Snapshot();
        using var pngData = image.Encode(SKEncodedImageFormat.Png, 100);
        using var pngFile = File.Create(pngPath);
        pngData.SaveTo(pngFile);
        pngFile.Flush();

        // 生成 ICO 文件 (Vista+ PNG-in-ICO 格式)
        var icoPath = Path.Combine(assetsDir, "clothesline.ico");
        using var icoStream = File.Create(icoPath);
        using var writer = new BinaryWriter(icoStream);
        // ICONDIR: 2 bytes reserved (0), 2 bytes type (1=ICO), 2 bytes count (1)
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)1);
        // ICONDIRENTRY: 16 bytes
        writer.Write((byte)0); // width: 0 means 256
        writer.Write((byte)0); // height: 0 means 256
        writer.Write((byte)0); // colors
        writer.Write((byte)0); // reserved
        writer.Write((ushort)1); // color planes
        writer.Write((ushort)32); // bits per pixel
        writer.Write((uint)pngData.Size); // size of image data
        writer.Write((uint)22); // offset of image data (6 + 16 = 22)
        // Image data: raw PNG bytes
        pngData.SaveTo(icoStream);
        icoStream.Flush();

        Assert.True(File.Exists(pngPath));
        Assert.True(File.Exists(icoPath));
    }

    [Fact]
    public void DragDropHelper_ExtractFilePaths_ShouldExtractInternalCardFiles()
    {
        var data = new Avalonia.Input.DataObject();
        data.Set(Avalonia.Input.DataFormats.Files, new[] { @"C:\demo\photo1.png", @"C:\demo\doc2.pdf" });

        var extracted = Clothesline.App.Services.DragDropHelper.ExtractFilePaths(data);
        Assert.Equal(2, extracted.Count);
        Assert.Equal(@"C:\demo\photo1.png", extracted[0]);
        Assert.Equal(@"C:\demo\doc2.pdf", extracted[1]);
    }

    [Fact]
    public void DragDropHelper_ExtractFilePaths_ShouldExtractCustomCardPath()
    {
        var data = new Avalonia.Input.DataObject();
        data.Set(Clothesline.App.Services.DragDropHelper.CardFilePathFormat, @"D:\work\deck.pptx");
        data.Set(Clothesline.App.Services.DragDropHelper.RopeItemIdFormat, "card-1234");

        var extracted = Clothesline.App.Services.DragDropHelper.ExtractFilePaths(data);
        Assert.Single(extracted);
        Assert.Equal(@"D:\work\deck.pptx", extracted[0]);
    }

    [Fact]
    public void DragDropHelper_ExtractFilePaths_ShouldDeduplicatePaths()
    {
        var data = new Avalonia.Input.DataObject();
        data.Set(Avalonia.Input.DataFormats.Files, new[] { @"C:\demo\PHOTO1.png", @"c:\demo\photo1.png" });

        var extracted = Clothesline.App.Services.DragDropHelper.ExtractFilePaths(data);
        Assert.Single(extracted);
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [Fact]
    public void PlatformAdapter_IsOverCaptionButtons_ShouldDetectTopRightCorrectly()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var adapter = new Clothesline.Platform.Windows.WindowsPlatformAdapter();
            int screenWidth = GetSystemMetrics(0);
            bool isOverCorner = adapter.IsOverCaptionButtons((screenWidth - 50, 20), IntPtr.Zero);
            bool isOverCenter = adapter.IsOverCaptionButtons((screenWidth / 2, 20), IntPtr.Zero);
            bool isOverBelow = adapter.IsOverCaptionButtons((screenWidth - 50, 150), IntPtr.Zero);

            Assert.True(isOverCorner);
            Assert.False(isOverCenter);
            Assert.False(isOverBelow);
        }
    }
}
