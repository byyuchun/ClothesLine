using System;
using SkiaSharp;

namespace Clothesline.Engines.Helpers;

/// <summary>
/// 转换引擎专用的 CJK 中文字体解析器，确保 PPTX/Word/TXT 转 PDF 汉字保真不乱码
/// </summary>
public static class EngineFontHelper
{
    private static SKTypeface? _cachedCjkTypeface;

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

        _cachedCjkTypeface = SKFontManager.Default.MatchCharacter('中') ?? SKTypeface.Default;
        return _cachedCjkTypeface;
    }
}
