using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Clothesline.App.Services;
using Clothesline.App.Views;

namespace Clothesline.App;

public partial class App : Application
{
    private TrayManager? _trayManager;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 确保窗口收起或子窗口关闭时应用持续常驻后台（只有点击托盘退出时才终止进程）
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var shelf = new ShelfWindow();
            desktop.MainWindow = shelf;

            _trayManager = new TrayManager(shelf);

            desktop.Exit += (_, _) =>
            {
                _trayManager.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
