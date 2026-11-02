using System;
using SkiaSharp;

namespace Clothesline.App.Helpers;

/// <summary>
/// 跨平台高保真中文字体解析器（彻底根除 Windows/macOS/Linux 上的中文四方豆腐块 □□□□ 乱码）
/// </summary>
public static class FontHelper
{
    private static SKTypeface? _cachedCjkTypeface;

    /// <summary>
    /// 获取当前系统最优 CJK 中文矢量字形（优先选择微软雅黑、苹方、黑体、文泉驿、思源宋黑）
    /// </summary>
    public static SKTypeface GetCjkTypeface()
    {
        if (_cachedCjkTypeface != null) return _cachedCjkTypeface;

        string[] fontPriorityList =
        [
            "Microsoft YaHei UI",
            "Microsoft YaHei",
            "PingFang SC",
            "Hiragino Sans GB",
            "SimHei",
            "WenQuanYi Micro Hei",
            "Noto Sans CJK SC",
            "Source Han Sans SC",
            "DengXian",
            "Kaiti",
            "Arial Unicode MS"
        ];

        foreach (var fontName in fontPriorityList)
        {
            var tf = SKTypeface.FromFamilyName(fontName);
            if (tf != null && tf.FamilyName.Equals(fontName, StringComparison.OrdinalIgnoreCase))
            {
                _cachedCjkTypeface = tf;
                return tf;
            }
        }

        // 字符级终极探测
        _cachedCjkTypeface = SKFontManager.Default.MatchCharacter('中') ?? SKTypeface.Default;
        return _cachedCjkTypeface;
    }
}
