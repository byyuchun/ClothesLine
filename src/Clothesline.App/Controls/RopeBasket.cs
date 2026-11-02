using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Clothesline.Core.Physics;

namespace Clothesline.App.Controls;

/// <summary>
/// StyleKit 铅笔手绘风（Sketch Style）拟物藤编布艺洗衣篮
/// 具有梯形上宽下窄敞口、翻折纯棉白亚麻布内衬、双耳麻绳提手、细腻藤条交叉排线与牛皮纸便签角标
/// </summary>
public class RopeBasket : Control
{
    public static readonly StyledProperty<int> ItemCountProperty =
        AvaloniaProperty.Register<RopeBasket, int>(nameof(ItemCount), defaultValue: 0);

    public static readonly StyledProperty<bool> IsHighlightedProperty =
        AvaloniaProperty.Register<RopeBasket, bool>(nameof(IsHighlighted), defaultValue: false);

    public int ItemCount
    {
        get => GetValue(ItemCountProperty);
        set => SetValue(ItemCountProperty, value);
    }

    public bool IsHighlighted
    {
        get => GetValue(IsHighlightedProperty);
        set => SetValue(IsHighlightedProperty, value);
    }

    public readonly SpringMotion SwingMotion = new(response: 0.45, damping: 0.72, initial: 0.0);
    public readonly SpringMotion CounterMotion = new(response: 0.28, damping: 0.65, initial: 1.0);
    public readonly SpringMotion ScaleMotion = new(response: 0.28, damping: 0.65, initial: 1.0);

    public event EventHandler? Clicked;

    private bool _isHovered;

    static RopeBasket()
    {
        AffectsRender<RopeBasket>(ItemCountProperty, IsHighlightedProperty);
    }

    public RopeBasket()
    {
        Width = 100;
        Height = 106;
        ClipToBounds = false;
        Cursor = new Cursor(StandardCursorType.Hand);
        DragDrop.SetAllowDrop(this, true);
    }

    public void StepPhysics(double dt)
    {
        ScaleMotion.Target = IsHighlighted ? 1.16 : (_isHovered ? 1.05 : 1.0);

        bool wasSettled = SwingMotion.Settled && CounterMotion.Settled && ScaleMotion.Settled;
        SwingMotion.Step(dt);
        CounterMotion.Step(dt);
        ScaleMotion.Step(dt);

        if (!wasSettled || !SwingMotion.Settled || !CounterMotion.Settled || !ScaleMotion.Settled)
        {
            InvalidateVisual();
        }
    }

    /// <summary>
    /// 卡片解挂落入晾衣篮或拖拽收纳时触发的物理弹跳
    /// </summary>
    public void Bounce()
    {
        SwingMotion.Velocity = Math.Max(-32.0, SwingMotion.Velocity - 24.0);
        CounterMotion.Velocity = Math.Min(2.5, CounterMotion.Velocity + 1.8);
        InvalidateVisual();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _isHovered = true;
        SwingMotion.Velocity += 8.0;
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _isHovered = false;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        SwingMotion.Velocity += 14.0;
        InvalidateVisual();
        Clicked?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // 挂钩旋转与缩放轴心点：(Width / 2, 2)
        var pivot = new Point(Width / 2.0, 2.0);
        double angle = SwingMotion.Position;
        double scale = ScaleMotion.Position;

        using (context.PushTransform(Matrix.CreateTranslation(-pivot.X, -pivot.Y) *
                                     Matrix.CreateScale(scale, scale) *
                                     Matrix.CreateRotation(Math.PI * angle / 180.0) *
                                     Matrix.CreateTranslation(pivot.X, pivot.Y)))
        {
            var sketchBorder = new Pen(new SolidColorBrush(Color.Parse("#1E293B")), 1.8, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            var fineSketch = new Pen(new SolidColorBrush(Color.Parse("#1E293B")), 1.2, lineCap: PenLineCap.Round);
            var shadowBrush = new SolidColorBrush(Color.Parse("#1E293B"));

            // 1. 顶端手弯金属细铁丝挂钩 (挂在晾衣绳线上)
            var hookGeometry = new StreamGeometry();
            using (var sgc = hookGeometry.Open())
            {
                sgc.BeginFigure(new Point(pivot.X + 8, pivot.Y + 12), false);
                sgc.ArcTo(new Point(pivot.X - 8, pivot.Y + 2), new Size(8, 8), 0, false, SweepDirection.CounterClockwise);
                sgc.LineTo(new Point(pivot.X, pivot.Y + 22));
            }
            context.DrawGeometry(null, sketchBorder, hookGeometry);

            // 挂钩金属连接吊环
            context.DrawEllipse(new SolidColorBrush(Color.Parse("#FFFDF9")), sketchBorder, new Point(pivot.X, pivot.Y + 22), 3.5, 3.5);

            // 2. 左右两侧手工编织麻绳提手 (Braided Rope Handles)
            // 左侧提手
            var leftHandle = new StreamGeometry();
            using (var lhc = leftHandle.Open())
            {
                lhc.BeginFigure(new Point(18, 36), false);
                lhc.CubicBezierTo(new Point(6, 38), new Point(6, 52), new Point(20, 54));
            }
            context.DrawGeometry(null, new Pen(new SolidColorBrush(Color.Parse("#1E293B")), 2.4, lineCap: PenLineCap.Round), leftHandle);
            context.DrawGeometry(null, new Pen(new SolidColorBrush(Color.Parse("#E2CDAE")), 1.2, lineCap: PenLineCap.Round), leftHandle);

            // 右侧提手
            var rightHandle = new StreamGeometry();
            using (var rhc = rightHandle.Open())
            {
                rhc.BeginFigure(new Point(82, 36), false);
                rhc.CubicBezierTo(new Point(94, 38), new Point(94, 52), new Point(80, 54));
            }
            context.DrawGeometry(null, new Pen(new SolidColorBrush(Color.Parse("#1E293B")), 2.4, lineCap: PenLineCap.Round), rightHandle);
            context.DrawGeometry(null, new Pen(new SolidColorBrush(Color.Parse("#E2CDAE")), 1.2, lineCap: PenLineCap.Round), rightHandle);

            // 3. 梯形收口藤编筐主体 (上宽 66px, 下宽 50px, 高 64px)
            // 顶点坐标：左上(17, 28), 右上(83, 28), 右下(76, 88), 左下(24, 88)
            var basketBody = new StreamGeometry();
            using (var bc = basketBody.Open())
            {
                bc.BeginFigure(new Point(17, 28), true);
                bc.LineTo(new Point(83, 28));
                bc.LineTo(new Point(76, 88));
                bc.ArcTo(new Point(70, 93), new Size(5, 5), 0, false, SweepDirection.Clockwise);
                bc.LineTo(new Point(30, 93));
                bc.ArcTo(new Point(24, 88), new Size(5, 5), 0, false, SweepDirection.Clockwise);
                bc.LineTo(new Point(17, 28));
                bc.EndFigure(true);
            }

            // (A0) 拖拽靠近时暖阳手绘金光轮廓 (指示拖放即可收纳进篮)
            if (IsHighlighted)
            {
                var haloPen = new Pen(new SolidColorBrush(Color.FromArgb(150, 245, 158, 11)), 7.0, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                context.DrawGeometry(null, haloPen, basketBody);
                var innerHalo = new Pen(new SolidColorBrush(Color.FromArgb(200, 254, 240, 138)), 3.2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                context.DrawGeometry(null, innerHalo, basketBody);
            }

            // (A) 硬核手绘偏移阴影 (3.5px 偏移)
            var shadowBody = new StreamGeometry();
            using (var sbc = shadowBody.Open())
            {
                sbc.BeginFigure(new Point(20.5, 31.5), true);
                sbc.LineTo(new Point(86.5, 31.5));
                sbc.LineTo(new Point(79.5, 91.5));
                sbc.ArcTo(new Point(73.5, 96.5), new Size(5, 5), 0, false, SweepDirection.Clockwise);
                sbc.LineTo(new Point(33.5, 96.5));
                sbc.ArcTo(new Point(27.5, 91.5), new Size(5, 5), 0, false, SweepDirection.Clockwise);
                sbc.LineTo(new Point(20.5, 31.5));
                sbc.EndFigure(true);
            }
            context.DrawGeometry(shadowBrush, null, shadowBody);

            // (B) 暖色竹藤编织底色 (高亮时变亮为阳光暖麦金色)
            string fillColor = IsHighlighted ? "#FFF1D6" : (_isHovered ? "#FDF8F0" : "#F4ECE1");
            var basketFill = new SolidColorBrush(Color.Parse(fillColor));
            context.DrawGeometry(basketFill, sketchBorder, basketBody);

            // 4. 细腻交错竹藤素描编织排线 (Intricate Wicker Weaving)
            var hatchBrush = new SolidColorBrush(Color.FromArgb(95, 30, 41, 59));
            var hatchPen = new Pen(hatchBrush, 1.1, lineCap: PenLineCap.Round);
            var shadowHatchPen = new Pen(new SolidColorBrush(Color.FromArgb(130, 30, 41, 59)), 1.1, lineCap: PenLineCap.Round);

            // 横向加强编织肋条
            double[] ribs = [48.0, 60.0, 72.0, 84.0];
            foreach (double ribY in ribs)
            {
                // 计算当前高度下的梯形左右边界
                double progress = (ribY - 28.0) / (93.0 - 28.0);
                double leftX = 17.0 + (24.0 - 17.0) * progress;
                double rightX = 83.0 - (83.0 - 76.0) * progress;

                // 略带自然弧度的编织横梁
                var ribPath = new StreamGeometry();
                using (var rpc = ribPath.Open())
                {
                    rpc.BeginFigure(new Point(leftX + 2, ribY), false);
                    rpc.QuadraticBezierTo(new Point(50, ribY + 1.8), new Point(rightX - 2, ribY));
                }
                context.DrawGeometry(null, fineSketch, ribPath);

                // 交叉藤条手绘针脚 (/// 与 \\\\)
                for (double hx = leftX + 4; hx <= rightX - 6; hx += 8.5)
                {
                    context.DrawLine(hatchPen, new Point(hx, ribY - 4.5), new Point(hx + 4.5, ribY + 4.5));
                    context.DrawLine(hatchPen, new Point(hx + 4.5, ribY - 4.5), new Point(hx, ribY + 4.5));
                }
            }

            // 左右两侧圆弧明暗交界手绘排线
            for (double sy = 44; sy <= 88; sy += 5)
            {
                context.DrawLine(shadowHatchPen, new Point(22, sy), new Point(28, sy + 3));
                context.DrawLine(shadowHatchPen, new Point(78, sy), new Point(72, sy + 3));
            }

            // 5. 顶部翻折纯棉白亚麻布内衬 (Turned-Over Linen Liner)
            // 盖在篮筐上沿，带有手绘自然波浪垂坠折痕
            var linerGeometry = new StreamGeometry();
            using (var lgc = linerGeometry.Open())
            {
                lgc.BeginFigure(new Point(14, 25), true);
                lgc.LineTo(new Point(86, 25));
                lgc.LineTo(new Point(84, 40));
                // 4 个柔和圆润的布褶下摆
                lgc.QuadraticBezierTo(new Point(75, 43), new Point(66, 40));
                lgc.QuadraticBezierTo(new Point(57, 43), new Point(48, 40));
                lgc.QuadraticBezierTo(new Point(39, 43), new Point(30, 40));
                lgc.QuadraticBezierTo(new Point(21, 43), new Point(16, 40));
                lgc.LineTo(new Point(14, 25));
                lgc.EndFigure(true);
            }

            // 布艺阴影
            var linerShadow = new StreamGeometry();
            using (var lsc = linerShadow.Open())
            {
                lsc.BeginFigure(new Point(16, 27), true);
                lsc.LineTo(new Point(88, 27));
                lsc.LineTo(new Point(86, 42));
                lsc.QuadraticBezierTo(new Point(77, 45), new Point(68, 42));
                lsc.QuadraticBezierTo(new Point(59, 45), new Point(50, 42));
                lsc.QuadraticBezierTo(new Point(41, 45), new Point(32, 42));
                lsc.QuadraticBezierTo(new Point(23, 45), new Point(18, 42));
                lsc.LineTo(new Point(16, 27));
                lsc.EndFigure(true);
            }
            context.DrawGeometry(shadowBrush, null, linerShadow);

            // 纯白亚麻布填充与墨线轮廓
            var linenFill = new SolidColorBrush(Color.Parse("#FFFDF9"));
            context.DrawGeometry(linenFill, sketchBorder, linerGeometry);

            // 布折素描阴影线与虚线针脚
            var stitchPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 30, 41, 59)), 1.0, lineCap: PenLineCap.Round);
            context.DrawLine(stitchPen, new Point(30, 31), new Point(30, 39));
            context.DrawLine(stitchPen, new Point(48, 31), new Point(48, 39));
            context.DrawLine(stitchPen, new Point(66, 31), new Point(66, 39));

            // 6. 悬挂于篮筐边缘的精巧手撕牛皮纸便签计数角标 (Paper Tag Badge)
            double badgeScale = Math.Clamp(CounterMotion.Position, 0.5, 2.2);
            var badgePivot = new Point(78, 22);

            using (context.PushTransform(Matrix.CreateTranslation(-badgePivot.X, -badgePivot.Y) *
                                         Matrix.CreateScale(badgeScale, badgeScale) *
                                         Matrix.CreateTranslation(badgePivot.X, badgePivot.Y)))
            {
                string text = ItemCount > 99 ? "99+" : ItemCount.ToString();
                var formatted = new FormattedText(
                    text,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.Bold),
                    11.0,
                    new SolidColorBrush(Color.Parse("#1E293B")));

                double badgeWidth = Math.Max(22, formatted.Width + 12);
                double badgeHeight = 20;
                var badgeRect = new Rect(badgePivot.X - badgeWidth / 2.0, badgePivot.Y - badgeHeight / 2.0, badgeWidth, badgeHeight);

                // 便签角标硬投影 (2.5px 偏移)
                context.DrawRectangle(shadowBrush, null, new Rect(badgeRect.X + 2.5, badgeRect.Y + 2.5, badgeWidth, badgeHeight), 5, 5);

                // 便签底色：有收纳项时呈现明快的暖象牙便签白 (#FFFDF9)，空时呈现低调浅米色 (#EFE7DC)
                var badgeBg = ItemCount > 0
                    ? new SolidColorBrush(Color.Parse("#FFFDF9"))
                    : new SolidColorBrush(Color.Parse("#EFE7DC"));

                context.DrawRectangle(badgeBg, sketchBorder, badgeRect, 5, 5);

                // 便签左上方小系绳孔 (Tag Eyelet)
                context.DrawEllipse(new SolidColorBrush(Color.Parse("#1E293B")), null, new Point(badgeRect.X + 4.5, badgeRect.Y + 4.5), 1.2, 1.2);

                // 居中绘制清晰数字
                double textX = badgeRect.X + (badgeWidth - formatted.Width) / 2.0;
                double textY = badgeRect.Y + (badgeHeight - formatted.Height) / 2.0;
                context.DrawText(formatted, new Point(textX, textY));
            }
        }
    }
}
