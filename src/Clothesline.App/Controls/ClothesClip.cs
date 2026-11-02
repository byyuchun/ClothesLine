using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Clothesline.App.Controls;

/// <summary>
/// StyleKit 铅笔手绘风（Sketch Style）木质晾衣夹
/// </summary>
public class ClothesClip : Control
{
    public static readonly StyledProperty<bool> IsOpenedProperty =
        AvaloniaProperty.Register<ClothesClip, bool>(nameof(IsOpened), defaultValue: false);

    public static readonly StyledProperty<Color> ClipColorProperty =
        AvaloniaProperty.Register<ClothesClip, Color>(nameof(ClipColor), defaultValue: Color.Parse("#E8CEB5"));

    public bool IsOpened
    {
        get => GetValue(IsOpenedProperty);
        set => SetValue(IsOpenedProperty, value);
    }

    public Color ClipColor
    {
        get => GetValue(ClipColorProperty);
        set => SetValue(ClipColorProperty, value);
    }

    static ClothesClip()
    {
        AffectsRender<ClothesClip>(IsOpenedProperty, ClipColorProperty);
    }

    public ClothesClip()
    {
        Width = 16;
        Height = 32;
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // 旋转轴心点：夹子中央金属弹簧簧圈处 (8, 14)
        var pivot = new Point(8, 14);
        double angle = IsOpened ? 12.0 : 0.0;
        var outlinePen = new Pen(new SolidColorBrush(Color.Parse("#1E293B")), 1.2);

        // 1. 绘制后夹臂（向左微旋，铅笔石墨硬边勾勒）
        using (context.PushTransform(Matrix.CreateTranslation(-pivot.X, -pivot.Y) *
                                     Matrix.CreateRotation(Math.PI * -angle / 180.0) *
                                     Matrix.CreateTranslation(pivot.X, pivot.Y)))
        {
            var rearBrush = new SolidColorBrush(Color.Parse("#D4BA9F"));
            context.DrawRectangle(rearBrush, outlinePen, new Rect(3, 2, 9, 28), 2, 2);

            // 木质素描斜排线
            var hatchPen = new Pen(new SolidColorBrush(Color.FromArgb(50, 30, 41, 59)), 0.8);
            context.DrawLine(hatchPen, new Point(4, 8), new Point(10, 14));
            context.DrawLine(hatchPen, new Point(4, 18), new Point(10, 24));
        }

        // 2. 绘制前夹臂（向右微旋）
        using (context.PushTransform(Matrix.CreateTranslation(-pivot.X, -pivot.Y) *
                                     Matrix.CreateRotation(Math.PI * angle / 180.0) *
                                     Matrix.CreateTranslation(pivot.X, pivot.Y)))
        {
            var frontBrush = new SolidColorBrush(ClipColor);
            context.DrawRectangle(frontBrush, outlinePen, new Rect(4, 2, 8, 28), 2, 2);

            // 前臂高光细条与素描排线
            var woodHatch = new Pen(new SolidColorBrush(Color.FromArgb(60, 30, 41, 59)), 0.8);
            context.DrawLine(woodHatch, new Point(5.5, 6), new Point(9.5, 10));
            context.DrawLine(woodHatch, new Point(5.5, 20), new Point(9.5, 24));
        }

        // 3. 绘制中央金属扭簧环 (铅笔勾勒扭簧与圆芯)
        var springBrush = new SolidColorBrush(Color.Parse("#FFFDF9"));
        var springPen = new Pen(new SolidColorBrush(Color.Parse("#1E293B")), 1.3);
        context.DrawEllipse(springBrush, springPen, new Point(8, 14), 4.5, 4.5);
        context.DrawEllipse(new SolidColorBrush(Color.Parse("#1E293B")), null, new Point(8, 14), 2.0, 2.0);
    }
}
