namespace Clothesline.Core.Models;

/// <summary>
/// 卡片右下角常驻格式胶囊徽章的样式定义
/// </summary>
public record BadgeStyle(
    string Text,
    string BackgroundColorHex,
    string ForegroundColorHex = "#FFFFFF"
);
