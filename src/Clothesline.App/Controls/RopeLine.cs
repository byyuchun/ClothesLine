using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Clothesline.App.Controls;

/// <summary>
/// StyleKit 铅笔手绘风（Sketch Style）贝塞尔物理晾衣绳
/// </summary>
public class RopeLine : Control
{
    public static readonly StyledProperty<double> SagProperty =
        AvaloniaProperty.Register<RopeLine, double>(nameof(Sag), defaultValue: 18.0);

    public static readonly StyledProperty<Color> RopeColorProperty =
        AvaloniaProperty.Register<RopeLine, Color>(nameof(RopeColor), defaultValue: Color.Parse("#8B7355"));

    public static readonly StyledProperty<double> RopeThicknessProperty =
        AvaloniaProperty.Register<RopeLine, double>(nameof(RopeThickness), defaultValue: 3.2);

    public static readonly StyledProperty<bool> IsHighlightedProperty =
        AvaloniaProperty.Register<RopeLine, bool>(nameof(IsHighlighted), defaultValue: false);

    public double Sag
    {
        get => GetValue(SagProperty);
        set => SetValue(SagProperty, value);
    }

    public Color RopeColor
    {
        get => GetValue(RopeColorProperty);
        set => SetValue(RopeColorProperty, value);
    }

    public double RopeThickness
    {
        get => GetValue(RopeThicknessProperty);
        set => SetValue(RopeThicknessProperty, value);
    }

    public bool IsHighlighted
    {
        get => GetValue(IsHighlightedProperty);
        set => SetValue(IsHighlightedProperty, value);
    }

    static RopeLine()
    {
        AffectsRender<RopeLine>(SagProperty, RopeColorProperty, RopeThicknessProperty, IsHighlightedProperty);
    }

    public RopeLine()
    {
        IsHitTestVisible = false;
    }

    /// <summary>
    /// 根据卡片横坐标获取当前绳索的精确物理 Y 高度
    /// </summary>
    public double GetRopeYAt(double x, double totalWidth)
    {
        if (totalWidth <= 0) return 18.0;
        double fraction = Math.Clamp(x / totalWidth, 0.0, 1.0);
        // 抛物线下垂公式: 4 * Sag * fraction * (1 - fraction)
        double sagOffset = 4.0 * Sag * fraction * (1.0 - fraction);
        return 18.0 + sagOffset;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double width = Bounds.Width;
        if (width <= 0) return;

        double startY = 18.0;
        double sag = Sag;
        var startPoint = new Point(0, startY);
        var endPoint = new Point(width, startY);
        var controlPoint = new Point(width / 2.0, startY + 2.0 * sag);

        // 主麻绳几何路径
        var outlineGeometry = new StreamGeometry();
        using (var ctx = outlineGeometry.Open())
        {
            ctx.BeginFigure(startPoint, false);
            ctx.QuadraticBezierTo(controlPoint, endPoint);
            ctx.EndFigure(false);
        }

        // 0. 拖拽悬停高亮提示：温暖手绘素描金黄光晕 (优雅指示可挂绳有效区域)
        if (IsHighlighted)
        {
            var glowPen = new Pen(new SolidColorBrush(Color.FromArgb(140, 245, 158, 11)), RopeThickness + 8.0, lineCap: PenLineCap.Round);
            context.DrawGeometry(null, glowPen, outlineGeometry);
            var innerGlowPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 254, 240, 138)), RopeThickness + 3.2, lineCap: PenLineCap.Round);
            context.DrawGeometry(null, innerGlowPen, outlineGeometry);
        }

        // 1. 底层硬边缘手绘阴影 (3px 偏移)
        var shadowGeometry = new StreamGeometry();
        using (var ctx = shadowGeometry.Open())
        {
            ctx.BeginFigure(new Point(0, startY + 2.2), false);
            ctx.QuadraticBezierTo(new Point(controlPoint.X, controlPoint.Y + 2.2), new Point(endPoint.X, endPoint.Y + 2.2));
            ctx.EndFigure(false);
        }
        var shadowPen = new Pen(new SolidColorBrush(Color.FromArgb(45, 30, 41, 59)), RopeThickness, lineCap: PenLineCap.Round);
        context.DrawGeometry(null, shadowPen, shadowGeometry);

        // 2. 主麻绳石墨勾线 (手绘深铅笔灰)
        var outlinePen = new Pen(new SolidColorBrush(Color.Parse("#1E293B")), RopeThickness + (IsHighlighted ? 1.8 : 1.2), lineCap: PenLineCap.Round);
        context.DrawGeometry(null, outlinePen, outlineGeometry);

        // 3. 绳身双股天然麻芯 (暖麻色 #D4BA9F，高亮时微亮变金黄麦草色)
        var coreBrush = new SolidColorBrush(IsHighlighted ? Color.Parse("#FFF0D4") : Color.Parse("#E2CDAE"));
        var corePen = new Pen(coreBrush, RopeThickness - (IsHighlighted ? 0.4 : 0.8), lineCap: PenLineCap.Round);
        context.DrawGeometry(null, corePen, outlineGeometry);

        // 4. 麻绳螺旋手绘断线点缀 (素描拧绳排线)
        var twistGeometry = new StreamGeometry();
        using (var ctx = twistGeometry.Open())
        {
            ctx.BeginFigure(new Point(0, startY - 0.4), false);
            ctx.QuadraticBezierTo(new Point(controlPoint.X, controlPoint.Y - 0.4), new Point(endPoint.X, endPoint.Y - 0.4));
            ctx.EndFigure(false);
        }
        var twistPen = new Pen(new SolidColorBrush(Color.Parse("#1E293B")), 1.0, lineCap: PenLineCap.Round,
            dashStyle: new DashStyle(new double[] { 4, 6 }, 0));
        context.DrawGeometry(null, twistPen, twistGeometry);
    }
}
