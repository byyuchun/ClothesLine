using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Clothesline.App.Controls;

/// <summary>
/// StyleKit 铅笔手绘风（Sketch Style）矢量圆环进度控件
/// </summary>
public class CircularProgress : Control
{
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<CircularProgress, double>(nameof(Progress), defaultValue: 0.0);

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<CircularProgress, double>(nameof(StrokeThickness), defaultValue: 3.5);

    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, Math.Clamp(value, 0.0, 1.0));
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    static CircularProgress()
    {
        AffectsRender<CircularProgress>(ProgressProperty, StrokeThicknessProperty);
    }

    public CircularProgress()
    {
        Width = 44;
        Height = 44;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        double stroke = StrokeThickness;
        var center = new Point(w / 2.0, h / 2.0);
        double radius = Math.Max(2, Math.Min(w, h) / 2.0 - stroke);

        // 1. 底层素描暗轨 (铅笔淡灰石墨浅线)
        var trackPen = new Pen(new SolidColorBrush(Color.Parse("#E2D9CC")), stroke);
        context.DrawEllipse(null, trackPen, center, radius, radius);

        // 2. 动态进度扫掠环 (石墨深黑铅笔扫掠)
        double p = Math.Clamp(Progress, 0.0, 1.0);
        if (p <= 0.001) return;

        var progressBrush = new SolidColorBrush(Color.Parse("#1E293B"));
        var progressPen = new Pen(progressBrush, stroke, lineCap: PenLineCap.Round);

        if (p >= 0.999)
        {
            context.DrawEllipse(null, progressPen, center, radius, radius);
            return;
        }

        double sweepAngle = p * 360.0;
        double startAngleRad = -Math.PI / 2.0; // 从 12 点钟方向开始
        double endAngleRad = startAngleRad + (sweepAngle * Math.PI / 180.0);

        var startPoint = new Point(
            center.X + radius * Math.Cos(startAngleRad),
            center.Y + radius * Math.Sin(startAngleRad));

        var endPoint = new Point(
            center.X + radius * Math.Cos(endAngleRad),
            center.Y + radius * Math.Sin(endAngleRad));

        var geometry = new StreamGeometry();
        using (var sgc = geometry.Open())
        {
            sgc.BeginFigure(startPoint, false);
            bool isLargeArc = sweepAngle > 180.0;
            sgc.ArcTo(endPoint, new Size(radius, radius), 0, isLargeArc, SweepDirection.Clockwise);
        }

        context.DrawGeometry(null, progressPen, geometry);
    }
}
