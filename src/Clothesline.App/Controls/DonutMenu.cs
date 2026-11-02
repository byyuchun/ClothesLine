using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Clothesline.Core.Models;

namespace Clothesline.App.Controls;

public record DonutSlice(string TargetFormat, BadgeStyle Badge);

/// <summary>
/// 纯自绘现代 Donut 轮盘选择器与动态转换进度环
/// </summary>
public class DonutMenu : Control
{
    public static readonly StyledProperty<double> OuterRadiusProperty =
        AvaloniaProperty.Register<DonutMenu, double>(nameof(OuterRadius), defaultValue: 140.0);

    public static readonly StyledProperty<double> InnerRadiusProperty =
        AvaloniaProperty.Register<DonutMenu, double>(nameof(InnerRadius), defaultValue: 62.0);

    public double OuterRadius
    {
        get => GetValue(OuterRadiusProperty);
        set => SetValue(OuterRadiusProperty, value);
    }

    public double InnerRadius
    {
        get => GetValue(InnerRadiusProperty);
        set => SetValue(InnerRadiusProperty, value);
    }

    private readonly List<DonutSlice> _slices = [];
    public IReadOnlyList<DonutSlice> Slices => _slices;

    private int _hoveredIndex = -1;
    public int HoveredIndex => _hoveredIndex;

    public bool IsConverting { get; private set; }
    public double Progress { get; private set; }
    public bool IsSuccess { get; private set; }
    public string? SelectedTargetFormat => _hoveredIndex >= 0 && _hoveredIndex < _slices.Count ? _slices[_hoveredIndex].TargetFormat : null;

    public event EventHandler<string>? SliceSelected;

    public DonutMenu()
    {
        Width = 320;
        Height = 320;
    }

    public void SetFormats(IEnumerable<string> targetFormats)
    {
        _slices.Clear();
        foreach (var format in targetFormats)
        {
            _slices.Add(new DonutSlice(format, BadgePalette.Resolve(format)));
        }
        _hoveredIndex = -1;
        IsConverting = false;
        Progress = 0.0;
        IsSuccess = false;
        InvalidateVisual();
    }

    public void StartConverting(string targetFormat)
    {
        IsConverting = true;
        Progress = 0.0;
        IsSuccess = false;
        InvalidateVisual();
    }

    public void UpdateProgress(double progress)
    {
        Progress = Math.Clamp(progress, 0.0, 1.0);
        InvalidateVisual();
    }

    public void MarkSuccess()
    {
        IsConverting = true;
        Progress = 1.0;
        IsSuccess = true;
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (IsConverting || _slices.Count == 0) return;

        var pt = e.GetPosition(this);
        var center = new Point(Bounds.Width / 2.0, Bounds.Height / 2.0);

        double dx = pt.X - center.X;
        double dy = pt.Y - center.Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);

        int newHover = -1;
        if (dist >= InnerRadius - 8 && dist <= OuterRadius + 14)
        {
            // 计算从顶端（12点钟方向）开始的顺时针角度
            double angle = Math.Atan2(dy, dx) + Math.PI / 2.0;
            if (angle < 0) angle += 2.0 * Math.PI;

            double span = 2.0 * Math.PI / _slices.Count;
            newHover = (int)(angle / span);
            newHover = Math.Clamp(newHover, 0, _slices.Count - 1);
        }

        if (_hoveredIndex != newHover)
        {
            _hoveredIndex = newHover;
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (IsConverting || _hoveredIndex < 0 || _hoveredIndex >= _slices.Count) return;

        var selected = _slices[_hoveredIndex].TargetFormat;
        SliceSelected?.Invoke(this, selected);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double w = Bounds.Width;
        double h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        var center = new Point(w / 2.0, h / 2.0);

        // 1. 转换中模式：渲染动态进度圆环
        if (IsConverting)
        {
            RenderProgressRing(context, center);
            return;
        }

        // 2. 切片选择模式：渲染 Donut 切片环
        if (_slices.Count == 0) return;

        int count = _slices.Count;
        double span = 2.0 * Math.PI / count;
        double gapAngle = count > 1 ? 0.035 : 0.0;

        for (int i = 0; i < count; i++)
        {
            var slice = _slices[i];
            bool isHovered = i == _hoveredIndex;

            double r1 = InnerRadius;
            double r2 = isHovered ? OuterRadius + 8.0 : OuterRadius;

            double startAngle = i * span - Math.PI / 2.0 + gapAngle;
            double endAngle = (i + 1) * span - Math.PI / 2.0 - gapAngle;
            double midAngle = (startAngle + endAngle) / 2.0;

            // 绘制切片扇区几何体
            var wedgeGeometry = BuildWedgeGeometry(center, r1, r2, startAngle, endAngle);

            var baseColor = Color.Parse(slice.Badge.BackgroundColorHex);
            var sliceColor = isHovered
                ? Color.FromArgb(240, baseColor.R, baseColor.G, baseColor.B)
                : Color.FromArgb(200, (byte)(baseColor.R * 0.85), (byte)(baseColor.G * 0.85), (byte)(baseColor.B * 0.85));

            var borderPen = isHovered
                ? new Pen(Brushes.White, 2.0)
                : new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 1.0);

            context.DrawGeometry(new SolidColorBrush(sliceColor), borderPen, wedgeGeometry);

            // 绘制切片中央格式文字 (全大写加粗)
            double textR = (r1 + r2) / 2.0;
            var textPoint = new Point(center.X + textR * Math.Cos(midAngle), center.Y + textR * Math.Sin(midAngle));

            var formattedText = new FormattedText(
                slice.TargetFormat,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold),
                14.0,
                Brushes.White
            );

            context.DrawText(formattedText, new Point(textPoint.X - formattedText.Width / 2.0, textPoint.Y - formattedText.Height / 2.0));
        }

        // 绘制中央半透明磨砂中心圆
        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(180, 15, 23, 42)), null, center, InnerRadius - 4, InnerRadius - 4);
    }

    private void RenderProgressRing(DrawingContext context, Point center)
    {
        double r = (InnerRadius + OuterRadius) / 2.0;
        double thickness = OuterRadius - InnerRadius;

        // 背景导轨环
        var bgPen = new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), thickness);
        context.DrawEllipse(null, bgPen, center, r, r);

        // 动态进度圆弧
        if (Progress > 0.01)
        {
            var progressGeometry = new StreamGeometry();
            using (var ctx = progressGeometry.Open())
            {
                double startAngle = -Math.PI / 2.0;
                double sweepAngle = Progress * 2.0 * Math.PI;
                double endAngle = startAngle + sweepAngle;

                var startPt = new Point(center.X + r * Math.Cos(startAngle), center.Y + r * Math.Sin(startAngle));
                var endPt = new Point(center.X + r * Math.Cos(endAngle), center.Y + r * Math.Sin(endAngle));

                ctx.BeginFigure(startPt, false);
                ctx.ArcTo(endPt, new Size(r, r), 0, sweepAngle > Math.PI, SweepDirection.Clockwise);
                ctx.EndFigure(false);
            }

            var progressPen = new Pen(new SolidColorBrush(Color.Parse("#10B981")), thickness, lineCap: PenLineCap.Round);
            context.DrawGeometry(null, progressPen, progressGeometry);
        }

        // 中心状态呈现
        if (IsSuccess)
        {
            var checkText = new FormattedText(
                "✓",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold),
                36.0,
                new SolidColorBrush(Color.Parse("#10B981"))
            );
            context.DrawText(checkText, new Point(center.X - checkText.Width / 2.0, center.Y - checkText.Height / 2.0));
        }
        else
        {
            var percentText = new FormattedText(
                $"{Progress * 100:0}%",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold),
                20.0,
                Brushes.White
            );
            context.DrawText(percentText, new Point(center.X - percentText.Width / 2.0, center.Y - percentText.Height / 2.0));
        }
    }

    private static StreamGeometry BuildWedgeGeometry(Point c, double r1, double r2, double a1, double a2)
    {
        var geom = new StreamGeometry();
        using var ctx = geom.Open();

        var p1 = new Point(c.X + r2 * Math.Cos(a1), c.Y + r2 * Math.Sin(a1));
        var p2 = new Point(c.X + r2 * Math.Cos(a2), c.Y + r2 * Math.Sin(a2));
        var p3 = new Point(c.X + r1 * Math.Cos(a2), c.Y + r1 * Math.Sin(a2));
        var p4 = new Point(c.X + r1 * Math.Cos(a1), c.Y + r1 * Math.Sin(a1));

        ctx.BeginFigure(p1, true);
        ctx.ArcTo(p2, new Size(r2, r2), 0, (a2 - a1) > Math.PI, SweepDirection.Clockwise);
        ctx.LineTo(p3);
        ctx.ArcTo(p4, new Size(r1, r1), 0, (a2 - a1) > Math.PI, SweepDirection.CounterClockwise);
        ctx.EndFigure(true);

        return geom;
    }
}
