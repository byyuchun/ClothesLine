using Avalonia;
using Clothesline.Platform.SingleInstance;

namespace Clothesline.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // 单实例检查：若已有运行中的实例，则静默退出
        using var singleInstance = new SingleInstanceLock();
        if (!singleInstance.IsOnlyInstance)
        {
            return;
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
