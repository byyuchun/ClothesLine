using System.Diagnostics;
using Clothesline.Platform.Abstractions;

namespace Clothesline.Platform.MacOS;

/// <summary>
/// macOS 平台适配层（支持访达定位、关联程序打开与剪贴板桥接）
/// </summary>
public class MacOSPlatformAdapter : IPlatformAdapter
{
    public (int X, int Y) GetCursorPosition()
    {
        // Avalonia 在 macOS 下由 PointerMoved 事件直接提供坐标，此处提供全局回退
        return (0, 0);
    }

    public void PrepareOverlay(IntPtr windowHandle)
    {
        // macOS 下由窗口属性处理
    }

    public void SetWindowPassMouse(IntPtr windowHandle, bool passMouse)
    {
        // macOS 下通过 NSWindow.ignoresMouseEvents 处理
    }

    public bool IsOverCaptionButtons((int X, int Y) cursor, IntPtr overlayWindow)
    {
        // macOS 窗口红绿灯按钮位于左上角 (X <= 80, Y <= 30)
        return cursor.X <= 80 && cursor.Y <= 30;
    }

    public bool IsLeftMouseButtonDown()
    {
        return false;
    }

    public bool TrySaveClipboardImage(string targetPngPath)
    {
        return false;
    }

    public void OpenFile(string filePath)
    {
        if (!File.Exists(filePath)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "open",
                Arguments = $"\"{filePath}\"",
                UseShellExecute = false
            });
        }
        catch
        {
            // 忽略打开异常
        }
    }

    public void LocateInFileManager(string filePath)
    {
        if (!File.Exists(filePath)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "open",
                Arguments = $"-R \"{filePath}\"",
                UseShellExecute = false
            });
        }
        catch
        {
            // 忽略访达启动异常
        }
    }

    public bool IsFullScreenAppActive()
    {
        // macOS 全屏 Space 判定
        return false;
    }

    public void CopyFilesToClipboard(IReadOnlyList<string> filePaths)
    {
        var validFiles = filePaths.Where(File.Exists).ToList();
        if (validFiles.Count == 0) return;

        try
        {
            // 利用 macOS 内置 osascript 将 POSIX 路径列表压入剪贴板
            var script = string.Join(", ", validFiles.Select(p => $"POSIX file \"{p.Replace("\"", "\\\"")}\""));
            var appleScript = $"set the clipboard to {{{script}}}";

            var psi = new ProcessStartInfo
            {
                FileName = "osascript",
                ArgumentList = { "-e", appleScript },
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(1000);
        }
        catch
        {
            // 忽略剪贴板写入异常
        }
    }
}
