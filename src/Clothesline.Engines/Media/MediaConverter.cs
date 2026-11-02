using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Clothesline.Engines.Abstractions;

namespace Clothesline.Engines.Media;

/// <summary>
/// 原生音视频媒体提取引擎（支持 MP4 / MOV / MKV / WMV / AVI 一键提取 MP3 / WAV 伴音）
/// 基于 Windows 原生 MediaTranscoder 与跨平台管道，免除对庞大外部第三方 ffmpeg 的硬性依赖。
/// </summary>
public class MediaConverter : IConverter
{
    private static readonly HashSet<string> SupportedSources = new(StringComparer.OrdinalIgnoreCase)
    {
        "MP4", "MOV", "MKV", "WMV", "AVI", "WEBM", "M4V"
    };

    private static readonly HashSet<string> SupportedTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        "MP3", "WAV"
    };

    public bool CanConvert(string sourceExt, string targetExt)
    {
        var src = sourceExt.TrimStart('.').ToUpperInvariant();
        var tgt = targetExt.TrimStart('.').ToUpperInvariant();

        return SupportedSources.Contains(src) && SupportedTargets.Contains(tgt);
    }

    public async Task<string> ConvertAsync(
        string sourceFilePath,
        string targetExt,
        string outputDirectory,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(0.1);

        return await Task.Run(() =>
        {
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(sourceFilePath);
            var normalizedTargetExt = targetExt.TrimStart('.').ToLowerInvariant();
            var targetFileName = $"{fileNameWithoutExt}.{normalizedTargetExt}";
            var targetFilePath = Path.Combine(outputDirectory, targetFileName);

            if (File.Exists(targetFilePath))
            {
                targetFileName = $"{fileNameWithoutExt}_{DateTime.Now:HHmmss}.{normalizedTargetExt}";
                targetFilePath = Path.Combine(outputDirectory, targetFileName);
            }

            Directory.CreateDirectory(outputDirectory);
            progress?.Report(0.3);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ConvertViaWindowsTranscoder(sourceFilePath, targetFilePath, normalizedTargetExt, progress);
            }
            else
            {
                ConvertViaMac(sourceFilePath, targetFilePath, normalizedTargetExt, progress);
            }

            progress?.Report(1.0);
            return targetFilePath;
        }, cancellationToken);
    }

    private static void ConvertViaWindowsTranscoder(
        string sourceFilePath,
        string targetFilePath,
        string targetExt,
        IProgress<double>? progress)
    {
        progress?.Report(0.4);

        string profileMethod = targetExt == "wav" ? "CreateWav" : "CreateMp3";
        string escapedSource = sourceFilePath.Replace("'", "''");
        string escapedTarget = targetFilePath.Replace("'", "''");

        string script = $@"
$source = [Windows.Storage.StorageFile]::GetFileFromPathAsync('{escapedSource}').GetAwaiter().GetResult();
$target = [Windows.Storage.StorageFile]::CreateFileAsync('{escapedTarget}', 1).GetAwaiter().GetResult();
$transcoder = New-Object Windows.Media.Transcoding.MediaTranscoder;
$profile = [Windows.Media.MediaProperties.MediaEncodingProfile]::{profileMethod}([Windows.Media.MediaProperties.AudioEncodingQuality]::High);
$prep = $transcoder.PrepareFileTranscodeAsync($source, $target, $profile).GetAwaiter().GetResult();
if ($prep.CanTranscode) {{
    $prep.TranscodeAsync().GetAwaiter().GetResult();
}}
";

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };

        using var proc = Process.Start(psi);
        if (proc != null)
        {
            progress?.Report(0.7);
            proc.WaitForExit(15000);
        }

        if (!File.Exists(targetFilePath) || new FileInfo(targetFilePath).Length == 0)
        {
            throw new InvalidOperationException($"原生音频转码失败: {targetFilePath}");
        }
    }

    private static void ConvertViaMac(
        string sourceFilePath,
        string targetFilePath,
        string targetExt,
        IProgress<double>? progress)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "afconvert",
            Arguments = $"-f WAVE -d LEI16 \"{sourceFilePath}\" \"{targetFilePath}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi);
        proc?.WaitForExit(10000);
    }
}
