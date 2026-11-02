using System.Runtime.InteropServices;

namespace Clothesline.Core.Storage;

/// <summary>
/// 跨平台统一系统路径解析器（Windows & macOS）
/// </summary>
public static class AppPaths
{
    private static readonly string BaseDirectory;
    private static readonly string ConvertedDirectory;

    static AppPaths()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            BaseDirectory = Path.Combine(localAppData, "Clothesline");
            ConvertedDirectory = Path.Combine(BaseDirectory, "Inbox", "Converted");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            BaseDirectory = Path.Combine(home, "Library", "Application Support", "Clothesline");
            ConvertedDirectory = Path.Combine(home, "Library", "Caches", "Clothesline", "Converted");
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            BaseDirectory = Path.Combine(home, ".clothesline");
            ConvertedDirectory = Path.Combine(BaseDirectory, "converted");
        }
    }

    public static string DataDirectory => Path.Combine(BaseDirectory, "data");

    public static string StateFilePath => Path.Combine(DataDirectory, "state.json");

    public static string BasketFilePath => Path.Combine(DataDirectory, "basket_history.json");

    public static string PendingCleanupFilePath => Path.Combine(DataDirectory, "pending_cleanups.json");

    public static string ConvertedOutputDirectory => ConvertedDirectory;

    public static string ScreenshotsDirectory => Path.Combine(BaseDirectory, "Inbox", "Screenshots");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(ConvertedDirectory);
        Directory.CreateDirectory(ScreenshotsDirectory);
    }
}
