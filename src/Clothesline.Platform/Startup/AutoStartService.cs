using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Clothesline.Platform.Startup;

/// <summary>
/// 系统开机自动启动服务（Windows 注册表 Run 键管理 & macOS 适配）
/// </summary>
public static class AutoStartService
{
    private const string KeyName = "Clothesline";
    private const string WindowsRunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsAutoStartEnabled()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(WindowsRunRegistryPath, writable: false);
                var value = key?.GetValue(KeyName) as string;
                return !string.IsNullOrWhiteSpace(value);
            }
            catch
            {
                return false;
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var plistPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library/LaunchAgents/com.clothesline.app.plist");
            return File.Exists(plistPath);
        }

        return false;
    }

    public static void SetAutoStart(bool enable)
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            try
            {
                exePath = Process.GetCurrentProcess().MainModule?.FileName;
            }
            catch
            {
                // ignore
            }
        }

        if (string.IsNullOrWhiteSpace(exePath)) return;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(WindowsRunRegistryPath, writable: true);
                if (key == null) return;

                if (enable)
                {
                    key.SetValue(KeyName, $"\"{exePath}\"");
                }
                else
                {
                    key.DeleteValue(KeyName, throwOnMissingValue: false);
                }
            }
            catch
            {
                // 忽略权限或注册表异常
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            try
            {
                var agentsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library/LaunchAgents");
                var plistPath = Path.Combine(agentsDir, "com.clothesline.app.plist");

                if (enable)
                {
                    Directory.CreateDirectory(agentsDir);
                    var plistContent = $"""
                        <?xml version="1.0" encoding="UTF-8"?>
                        <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                        <plist version="1.0">
                        <dict>
                            <key>Label</key>
                            <string>com.clothesline.app</string>
                            <key>ProgramArguments</key>
                            <array>
                                <string>{exePath}</string>
                            </array>
                            <key>RunAtLoad</key>
                            <true/>
                        </dict>
                        </plist>
                        """;
                    File.WriteAllText(plistPath, plistContent);
                }
                else
                {
                    if (File.Exists(plistPath))
                    {
                        File.Delete(plistPath);
                    }
                }
            }
            catch
            {
                // 忽略配置异常
            }
        }
    }
}
