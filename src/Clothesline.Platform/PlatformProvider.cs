using System.Runtime.InteropServices;
using Clothesline.Platform.Abstractions;
using Clothesline.Platform.MacOS;
using Clothesline.Platform.Windows;

namespace Clothesline.Platform;

/// <summary>
/// 跨平台能力实例提供者（自动根据当前运行 OS 载入对应适配层）
/// </summary>
public static class PlatformProvider
{
    private static readonly Lazy<IPlatformAdapter> Instance = new(() =>
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsPlatformAdapter();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new MacOSPlatformAdapter();
        }

        // 默认回退
        return new WindowsPlatformAdapter();
    });

    public static IPlatformAdapter Current => Instance.Value;
}
