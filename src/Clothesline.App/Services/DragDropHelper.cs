using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Input;

namespace Clothesline.App.Services;

/// <summary>
/// 拖拽数据安全解析与跨平台格式兼容工具
/// </summary>
public static class DragDropHelper
{
    public const string RopeItemIdFormat = "Clothesline.RopeItemId";
    public const string CardFilePathFormat = "Clothesline.CardFilePath";

    /// <summary>
    /// 统一提取拖拽数据中的文件路径，兼容外部资源管理器 (IStorageItem) 与内部卡片拖拽 (string[] / FileNames / 自定义字段)
    /// </summary>
    public static List<string> ExtractFilePaths(IDataObject data)
    {
        var list = new List<string>();

        // 1. 优先读取 Avalonia 11 原生 IStorageItem (来自 Windows 资源管理器或桌面拖入)
        try
        {
            var storageFiles = data.GetFiles();
            if (storageFiles != null)
            {
                foreach (var sf in storageFiles)
                {
                    var local = sf.Path?.LocalPath;
                    if (!string.IsNullOrWhiteSpace(local) && !list.Contains(local, StringComparer.OrdinalIgnoreCase))
                    {
                        list.Add(local);
                    }
                }
            }
        }
        catch { }

        if (list.Count > 0) return list;

        // 2. 读取 DataFormats.Files (内存 string[] 或 IEnumerable<string>)
        try
        {
            var rawFiles = data.Get(DataFormats.Files);
            if (rawFiles is IEnumerable<string> strSeq)
            {
                foreach (var p in strSeq)
                {
                    if (!string.IsNullOrWhiteSpace(p) && !list.Contains(p, StringComparer.OrdinalIgnoreCase))
                    {
                        list.Add(p);
                    }
                }
            }
            else if (rawFiles is string singleStr && !string.IsNullOrWhiteSpace(singleStr))
            {
                list.Add(singleStr);
            }
        }
        catch { }

        if (list.Count > 0) return list;

        // 3. 读取 DataFormats.FileNames 或 "FileNames"
        try
        {
            var rawNames = data.Get(DataFormats.FileNames);
            if (rawNames is IEnumerable<string> names)
            {
                foreach (var p in names)
                {
                    if (!string.IsNullOrWhiteSpace(p) && !list.Contains(p, StringComparer.OrdinalIgnoreCase))
                    {
                        list.Add(p);
                    }
                }
            }
        }
        catch { }

        // 4. 读取自定义字段 Clothesline.CardFilePath
        try
        {
            var customPath = data.Get(CardFilePathFormat) as string;
            if (!string.IsNullOrWhiteSpace(customPath) && !list.Contains(customPath, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(customPath);
            }
        }
        catch { }

        return list;
    }
}
