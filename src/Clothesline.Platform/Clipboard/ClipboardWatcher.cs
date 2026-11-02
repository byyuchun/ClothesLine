using System.Runtime.InteropServices;
using Clothesline.Core.Storage;
using Clothesline.Platform.MacOS;
using Clothesline.Platform.Windows;

namespace Clothesline.Platform.Clipboard;

/// <summary>
/// 系统剪贴板截屏自动捕获监听器（继承 PicRope 经典截屏特性）
/// 当用户按下微信截图、Win+Shift+S 或复制位图时，自动生成 PNG 挂上晾衣绳
/// </summary>
public sealed class ClipboardWatcher : IDisposable
{
    private readonly Timer? _timer;
    private uint _lastSequenceNumber;
    private long _lastMacChangeCount;
    private bool _enabled = true;

    public bool IsEnabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public event EventHandler<string>? ImageCaptured;

    public ClipboardWatcher()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _lastSequenceNumber = Win32.GetClipboardSequenceNumber();
            // 300ms 轻量非阻塞心跳，0% CPU 占用
            _timer = new Timer(OnPollClipboard, null, 1000, 300);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // NSPasteboard.changeCount 读取开销极低，同样 300ms 轮询
            _lastMacChangeCount = MacPasteboard.ChangeCount();
            _timer = new Timer(OnPollClipboard, null, 1000, 300);
        }
    }

    private void OnPollClipboard(object? state)
    {
        if (!_enabled) return;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            uint currentSeq = Win32.GetClipboardSequenceNumber();
            if (currentSeq != _lastSequenceNumber && currentSeq != 0)
            {
                _lastSequenceNumber = currentSeq;
                CheckClipboardForImageWindows();
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            long count = MacPasteboard.ChangeCount();
            if (count != _lastMacChangeCount)
            {
                _lastMacChangeCount = count;
                SaveClipboardImage();
            }
        }
    }

    private void CheckClipboardForImageWindows()
    {
        try
        {
            // 检测剪贴板是否包含位图格式 (CF_BITMAP 或 CF_DIB)
            if (!Win32.IsClipboardFormatAvailable(Win32.CF_BITMAP) &&
                !Win32.IsClipboardFormatAvailable(Win32.CF_DIB))
            {
                return;
            }

            SaveClipboardImage();
        }
        catch
        {
            // 忽略剪贴板争用冲突
        }
    }

    private void SaveClipboardImage()
    {
        try
        {
            // 如果包含位图，暂存至临时文件并触发挂绳
            AppPaths.EnsureDirectories();
            var fileName = $"Capture_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.png";
            var filePath = Path.Combine(AppPaths.ScreenshotsDirectory, fileName);

            // 从原生剪贴板提取位图并保存为标准 PNG
            if (PlatformProvider.Current.TrySaveClipboardImage(filePath))
            {
                ImageCaptured?.Invoke(this, filePath);
            }
            else
            {
                // 去重或提取失败时清理可能残留的空文件
                try { if (File.Exists(filePath)) File.Delete(filePath); } catch { }
            }
        }
        catch
        {
            // 忽略剪贴板争用冲突
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
