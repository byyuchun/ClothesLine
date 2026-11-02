using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Clothesline.App.Helpers;
using SkiaSharp;
using AvRectangle = Avalonia.Controls.Shapes.Rectangle;
using AvLine = Avalonia.Controls.Shapes.Line;

namespace Clothesline.App.Views;

public partial class ImageEditorWindow : Window
{
    private enum EditorTool { Rect, Arrow, Pen, Mosaic, Text }

    private readonly string _sourceFilePath;
    private readonly Action<string, bool> _onSaved;

    private SKBitmap _currentBitmap = null!;
    private readonly Stack<SKBitmap> _undoStack = new();

    private EditorTool _currentTool = EditorTool.Rect;
    private SKColor _currentColor = new(0xE1, 0x1D, 0x48); // 默认手绘深红
    private float _strokeWidth = 3.5f;
    private float _zoomScale = 1.0f;

    private bool _isPointerDown;
    private Point _startPoint; // 控件内像素点
    private Point _lastPenPoint;

    // 就地打字浮动输入框状态
    private TextBox? _floatingTextBox;
    private Border? _floatingContainer;
    private Point _floatingTextBitmapPoint;

    public ImageEditorWindow() : this("", (_, _) => { })
    {
    }

    public ImageEditorWindow(string sourceFilePath, Action<string, bool> onSaved)
    {
        InitializeComponent();
        _sourceFilePath = sourceFilePath;
        _onSaved = onSaved;

        HeaderBar.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                CommitFloatingText();
                BeginMoveDrag(e);
            }
        };

        CloseButton.Click += (_, _) =>
        {
            CommitFloatingText();
            Close();
        };

        ToolRect.IsCheckedChanged += (_, _) => { if (ToolRect.IsChecked == true) SwitchTool(EditorTool.Rect); };
        ToolArrow.IsCheckedChanged += (_, _) => { if (ToolArrow.IsChecked == true) SwitchTool(EditorTool.Arrow); };
        ToolPen.IsCheckedChanged += (_, _) => { if (ToolPen.IsChecked == true) SwitchTool(EditorTool.Pen); };
        ToolMosaic.IsCheckedChanged += (_, _) => { if (ToolMosaic.IsChecked == true) SwitchTool(EditorTool.Mosaic); };
        ToolText.IsCheckedChanged += (_, _) => { if (ToolText.IsChecked == true) SwitchTool(EditorTool.Text); };

        ColorRed.Click += (_, _) => SetColor(new SKColor(0xE1, 0x1D, 0x48));
        ColorBlue.Click += (_, _) => SetColor(new SKColor(0x25, 0x63, 0xEB));
        ColorYellow.Click += (_, _) => SetColor(new SKColor(0xEA, 0xB3, 0x08));
        ColorGreen.Click += (_, _) => SetColor(new SKColor(0x05, 0x96, 0x69));
        ColorBlack.Click += (_, _) => SetColor(new SKColor(0x1E, 0x29, 0x3B));

        ZoomInBtn.Click += (_, _) => AdjustZoom(0.15f);
        ZoomOutBtn.Click += (_, _) => AdjustZoom(-0.15f);
        ZoomFitBtn.Click += (_, _) => FitToWindow();

        Scale50Btn.Click += (_, _) => ScaleBitmap(0.5f);
        Scale75Btn.Click += (_, _) => ScaleBitmap(0.75f);
        Scale150Btn.Click += (_, _) => ScaleBitmap(1.5f);
        Scale200Btn.Click += (_, _) => ScaleBitmap(2.0f);

        UndoButton.Click += (_, _) => PerformUndo();
        ClearButton.Click += (_, _) => ReloadOriginalImage();

        SaveOverwriteButton.Click += (_, _) =>
        {
            CommitFloatingText();
            SaveImage(overwrite: true);
        };
        SaveAsNewButton.Click += (_, _) =>
        {
            CommitFloatingText();
            SaveImage(overwrite: false);
        };

        MainCanvasImage.PointerPressed += OnCanvasPointerPressed;
        MainCanvasImage.PointerMoved += OnCanvasPointerMoved;
        MainCanvasImage.PointerReleased += OnCanvasPointerReleased;

        ImageScrollViewer.PointerWheelChanged += OnCanvasPointerWheel;

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                if (_floatingTextBox != null)
                {
                    CancelFloatingText();
                    e.Handled = true;
                    return;
                }
                Close();
            }

            if (e.Key == Key.Z && (e.KeyModifiers & KeyModifiers.Control) != 0)
            {
                CommitFloatingText();
                PerformUndo();
                e.Handled = true;
            }
        };

        LoadImage();
    }

    private void SwitchTool(EditorTool tool)
    {
        CommitFloatingText();
        _currentTool = tool;
        if (tool == EditorTool.Text)
        {
            StatusInfoText.Text = "文字工具已激活：在图上任意位置点击，光标原地闪烁即可打字";
        }
        else if (tool == EditorTool.Mosaic)
        {
            StatusInfoText.Text = "马赛克工具已激活：拖拽框选任意区域进行涂抹打码";
        }
        else
        {
            StatusInfoText.Text = "拖拽绘制批注 · 滚轮缩放画布 · Ctrl+Z撤销";
        }
    }

    private void SetColor(SKColor color)
    {
        _currentColor = color;
        if (_floatingTextBox != null)
        {
            _floatingTextBox.Foreground = new SolidColorBrush(Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue));
        }
    }

    private void LoadImage()
    {
        if (!File.Exists(_sourceFilePath)) return;

        using var inputStream = File.OpenRead(_sourceFilePath);
        using var codec = SKCodec.Create(inputStream);
        if (codec == null) return;

        _currentBitmap = SKBitmap.Decode(codec);
        DimensionText.Text = $"{_currentBitmap.Width} × {_currentBitmap.Height}";
        FitToWindow();
        UpdateDisplayImage();
    }

    private void ReloadOriginalImage()
    {
        CommitFloatingText();
        _undoStack.Clear();
        LoadImage();
    }

    private void PushUndoState()
    {
        if (_currentBitmap != null)
        {
            _undoStack.Push(_currentBitmap.Copy());
        }
    }

    private void PerformUndo()
    {
        CommitFloatingText();
        if (_undoStack.Count > 0)
        {
            _currentBitmap?.Dispose();
            _currentBitmap = _undoStack.Pop();
            DimensionText.Text = $"{_currentBitmap.Width} × {_currentBitmap.Height}";
            UpdateDisplayImage();
            ApplyZoom();
        }
    }

    private void AdjustZoom(float delta)
    {
        CommitFloatingText();
        _zoomScale = Math.Clamp(_zoomScale + delta, 0.15f, 4.0f);
        ApplyZoom();
    }

    private void FitToWindow()
    {
        if (_currentBitmap == null) return;
        CommitFloatingText();

        float availW = 980f;
        float availH = 580f;
        float scaleW = availW / _currentBitmap.Width;
        float scaleH = availH / _currentBitmap.Height;
        _zoomScale = Math.Clamp(Math.Min(1.0f, Math.Min(scaleW, scaleH)), 0.15f, 1.0f);
        ApplyZoom();
    }

    private void ApplyZoom()
    {
        if (_currentBitmap == null) return;
        double displayW = _currentBitmap.Width * _zoomScale;
        double displayH = _currentBitmap.Height * _zoomScale;

        MainCanvasImage.Width = displayW;
        MainCanvasImage.Height = displayH;
        OverlayCanvas.Width = displayW;
        OverlayCanvas.Height = displayH;
        TextInputOverlay.Width = displayW;
        TextInputOverlay.Height = displayH;
        CanvasPaperBorder.Width = displayW;
        CanvasPaperBorder.Height = displayH;

        ZoomText.Text = $"{(int)(_zoomScale * 100)}%";
    }

    private void ScaleBitmap(float factor)
    {
        if (_currentBitmap == null) return;
        CommitFloatingText();
        PushUndoState();

        int targetW = Math.Max(16, (int)(_currentBitmap.Width * factor));
        int targetH = Math.Max(16, (int)(_currentBitmap.Height * factor));

        var resized = _currentBitmap.Resize(new SKImageInfo(targetW, targetH), SKFilterQuality.High);
        if (resized != null)
        {
            _currentBitmap.Dispose();
            _currentBitmap = resized;
            DimensionText.Text = $"{_currentBitmap.Width} × {_currentBitmap.Height}";
            UpdateDisplayImage();
            ApplyZoom();
        }
    }

    private void OnCanvasPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        if ((e.KeyModifiers & KeyModifiers.Control) != 0 || e.KeyModifiers == KeyModifiers.None)
        {
            AdjustZoom((float)e.Delta.Y * 0.12f);
            e.Handled = true;
        }
    }

    private void UpdateDisplayImage()
    {
        if (_currentBitmap == null) return;

        using var image = SKImage.FromBitmap(_currentBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var ms = new MemoryStream(data.ToArray());
        MainCanvasImage.Source = new Bitmap(ms);
        ApplyZoom();
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_currentBitmap == null) return;
        var prop = e.GetCurrentPoint(MainCanvasImage).Properties;
        if (!prop.IsLeftButtonPressed) return;

        var clickPoint = e.GetPosition(MainCanvasImage);

        // 如果是文字工具，点击即在原地生成就地输入框
        if (_currentTool == EditorTool.Text)
        {
            CommitFloatingText();
            SpawnFloatingTextBox(clickPoint);
            e.Handled = true;
            return;
        }

        // 其他工具操作前先提交未完的文字
        CommitFloatingText();

        _isPointerDown = true;
        _startPoint = clickPoint;
        _lastPenPoint = _startPoint;

        PushUndoState();
        OverlayCanvas.Children.Clear();
        e.Handled = true;
    }

    private void SpawnFloatingTextBox(Point displayPoint)
    {
        _floatingTextBitmapPoint = new Point(displayPoint.X / _zoomScale, displayPoint.Y / _zoomScale);

        _floatingTextBox = new TextBox
        {
            MinWidth = 120,
            MaxWidth = 420,
            MinHeight = 30,
            FontSize = 14.5,
            FontWeight = FontWeight.SemiBold,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromArgb(_currentColor.Alpha, _currentColor.Red, _currentColor.Green, _currentColor.Blue)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 4),
            Watermark = "输入批注文字，按Enter确认",
            Cursor = new Cursor(StandardCursorType.Ibeam)
        };

        _floatingContainer = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#FFFDF9")),
            BorderBrush = new SolidColorBrush(Color.Parse("#1E293B")),
            BorderThickness = new Thickness(1.8),
            CornerRadius = new CornerRadius(4),
            BoxShadow = BoxShadows.Parse("2.5 2.5 0 0 #1E293B"),
            Child = _floatingTextBox
        };

        // 避免输入框超出右侧或底侧边界
        double left = Math.Min(displayPoint.X, Math.Max(0, TextInputOverlay.Width - 140));
        double top = Math.Min(displayPoint.Y, Math.Max(0, TextInputOverlay.Height - 40));

        Canvas.SetLeft(_floatingContainer, left);
        Canvas.SetTop(_floatingContainer, top);

        _floatingTextBox.KeyDown += (_, ke) =>
        {
            if (ke.Key == Key.Enter)
            {
                CommitFloatingText();
                ke.Handled = true;
            }
            else if (ke.Key == Key.Escape)
            {
                CancelFloatingText();
                ke.Handled = true;
            }
        };

        _floatingTextBox.LostFocus += (_, _) =>
        {
            CommitFloatingText();
        };

        TextInputOverlay.Children.Add(_floatingContainer);

        Dispatcher.UIThread.Post(() =>
        {
            _floatingTextBox?.Focus();
        }, DispatcherPriority.Input);
    }

    private void CommitFloatingText()
    {
        if (_floatingTextBox == null) return;

        string text = _floatingTextBox.Text?.Trim() ?? "";
        TextInputOverlay.Children.Clear();
        _floatingTextBox = null;
        _floatingContainer = null;

        if (string.IsNullOrEmpty(text) || _currentBitmap == null) return;

        PushUndoState();

        using var canvas = new SKCanvas(_currentBitmap);
        using var cjkTypeface = FontHelper.GetCjkTypeface();
        using var font = new SKFont(cjkTypeface, 20);
        using var textPaint = new SKPaint
        {
            Color = _currentColor,
            TextSize = 20,
            IsAntialias = true,
            Typeface = cjkTypeface
        };

        float textWidth = textPaint.MeasureText(text);
        float posX = (float)_floatingTextBitmapPoint.X;
        float posY = (float)_floatingTextBitmapPoint.Y + 20; // 基线对齐

        // StyleKit 素描手绘风便签纸底衬 (暖象牙白 + 石墨硬阴影边框)
        float padX = 8f;
        float padY = 5f;
        var labelRect = new SKRect(posX - padX, posY - 22 - padY, posX + textWidth + padX, posY + padY);

        // 1. 素描硬边缘投影
        using var shadowPaint = new SKPaint
        {
            Color = new SKColor(0x1E, 0x29, 0x3B),
            Style = SKPaintStyle.Fill,
            IsAntialias = false
        };
        var shadowRect = new SKRect(labelRect.Left + 2.5f, labelRect.Top + 2.5f, labelRect.Right + 2.5f, labelRect.Bottom + 2.5f);
        canvas.DrawRoundRect(new SKRoundRect(shadowRect, 4, 4), shadowPaint);

        // 2. 素描底纸
        using var paperPaint = new SKPaint
        {
            Color = new SKColor(0xFF, 0xFD, 0xF9),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(labelRect, 4, 4), paperPaint);

        // 3. 石墨边框
        using var borderPaint = new SKPaint
        {
            Color = new SKColor(0x1E, 0x29, 0x3B),
            StrokeWidth = 1.8f,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(labelRect, 4, 4), borderPaint);

        // 4. 高保真中文字符渲染（100% 拒绝豆腐块乱码）
        canvas.DrawText(text, posX, posY, font, textPaint);

        UpdateDisplayImage();
    }

    private void CancelFloatingText()
    {
        TextInputOverlay.Children.Clear();
        _floatingTextBox = null;
        _floatingContainer = null;
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPointerDown || _currentBitmap == null) return;

        var currentPoint = e.GetPosition(MainCanvasImage);

        if (_currentTool == EditorTool.Pen)
        {
            float lastX = (float)(_lastPenPoint.X / _zoomScale);
            float lastY = (float)(_lastPenPoint.Y / _zoomScale);
            float curX = (float)(currentPoint.X / _zoomScale);
            float curY = (float)(currentPoint.Y / _zoomScale);

            using var canvas = new SKCanvas(_currentBitmap);
            using var paint = new SKPaint
            {
                Color = _currentColor,
                StrokeWidth = _strokeWidth,
                IsAntialias = true,
                StrokeCap = SKStrokeCap.Round,
                Style = SKPaintStyle.Stroke
            };
            canvas.DrawLine(lastX, lastY, curX, curY, paint);
            _lastPenPoint = currentPoint;
            UpdateDisplayImage();
        }
        else if (_currentTool == EditorTool.Rect)
        {
            OverlayCanvas.Children.Clear();
            double x = Math.Min(_startPoint.X, currentPoint.X);
            double y = Math.Min(_startPoint.Y, currentPoint.Y);
            double w = Math.Abs(_startPoint.X - currentPoint.X);
            double h = Math.Abs(_startPoint.Y - currentPoint.Y);

            var rect = new AvRectangle
            {
                Width = w,
                Height = h,
                Stroke = new SolidColorBrush(Color.FromArgb(_currentColor.Alpha, _currentColor.Red, _currentColor.Green, _currentColor.Blue)),
                StrokeThickness = _strokeWidth,
                RadiusX = 4,
                RadiusY = 4
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            OverlayCanvas.Children.Add(rect);
        }
        else if (_currentTool == EditorTool.Arrow)
        {
            OverlayCanvas.Children.Clear();
            var line = new AvLine
            {
                StartPoint = _startPoint,
                EndPoint = currentPoint,
                Stroke = new SolidColorBrush(Color.FromArgb(_currentColor.Alpha, _currentColor.Red, _currentColor.Green, _currentColor.Blue)),
                StrokeThickness = _strokeWidth
            };
            OverlayCanvas.Children.Add(line);
        }
        else if (_currentTool == EditorTool.Mosaic)
        {
            OverlayCanvas.Children.Clear();
            double x = Math.Min(_startPoint.X, currentPoint.X);
            double y = Math.Min(_startPoint.Y, currentPoint.Y);
            double w = Math.Abs(_startPoint.X - currentPoint.X);
            double h = Math.Abs(_startPoint.Y - currentPoint.Y);

            var rect = new AvRectangle
            {
                Width = w,
                Height = h,
                Fill = new SolidColorBrush(Color.FromArgb(90, 56, 189, 248)),
                Stroke = new SolidColorBrush(Color.FromArgb(200, 30, 41, 59)),
                StrokeThickness = 1.5,
                StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 4, 3 }
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            OverlayCanvas.Children.Add(rect);
        }

        e.Handled = true;
    }

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPointerDown || _currentBitmap == null) return;
        _isPointerDown = false;
        OverlayCanvas.Children.Clear();

        var endPoint = e.GetPosition(MainCanvasImage);

        // 映射回原图物理像素坐标
        float bx1 = (float)(Math.Min(_startPoint.X, endPoint.X) / _zoomScale);
        float by1 = (float)(Math.Min(_startPoint.Y, endPoint.Y) / _zoomScale);
        float bx2 = (float)(Math.Max(_startPoint.X, endPoint.X) / _zoomScale);
        float by2 = (float)(Math.Max(_startPoint.Y, endPoint.Y) / _zoomScale);

        using var canvas = new SKCanvas(_currentBitmap);

        if (_currentTool == EditorTool.Rect)
        {
            if (Math.Abs(bx2 - bx1) > 2 || Math.Abs(by2 - by1) > 2)
            {
                using var paint = new SKPaint
                {
                    Color = _currentColor,
                    StrokeWidth = _strokeWidth,
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke
                };
                canvas.DrawRoundRect(new SKRoundRect(new SKRect(bx1, by1, bx2, by2), 4, 4), paint);
            }
        }
        else if (_currentTool == EditorTool.Arrow)
        {
            float startX = (float)(_startPoint.X / _zoomScale);
            float startY = (float)(_startPoint.Y / _zoomScale);
            float targetX = (float)(endPoint.X / _zoomScale);
            float targetY = (float)(endPoint.Y / _zoomScale);

            if (Math.Abs(targetX - startX) > 4 || Math.Abs(targetY - startY) > 4)
            {
                DrawArrow(canvas, startX, startY, targetX, targetY, _currentColor, _strokeWidth);
            }
        }
        else if (_currentTool == EditorTool.Mosaic)
        {
            ApplyMosaic((int)bx1, (int)by1, (int)(bx2 - bx1), (int)(by2 - by1));
        }

        UpdateDisplayImage();
        e.Handled = true;
    }

    private static void DrawArrow(SKCanvas canvas, float x1, float y1, float x2, float y2, SKColor color, float width)
    {
        using var linePaint = new SKPaint
        {
            Color = color,
            StrokeWidth = width,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
            Style = SKPaintStyle.Stroke
        };
        canvas.DrawLine(x1, y1, x2, y2, linePaint);

        // 计算箭头两个侧翼
        double angle = Math.Atan2(y2 - y1, x2 - x1);
        double arrowHeadAngle = Math.PI / 6.0; // 30 度
        double arrowLength = Math.Max(12.0, width * 4.0);

        float ax1 = (float)(x2 - arrowLength * Math.Cos(angle - arrowHeadAngle));
        float ay1 = (float)(y2 - arrowLength * Math.Sin(angle - arrowHeadAngle));
        float ax2 = (float)(x2 - arrowLength * Math.Cos(angle + arrowHeadAngle));
        float ay2 = (float)(y2 - arrowLength * Math.Sin(angle + arrowHeadAngle));

        using var headPaint = new SKPaint
        {
            Color = color,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
        using var path = new SKPath();
        path.MoveTo(x2, y2);
        path.LineTo(ax1, ay1);
        path.LineTo(ax2, ay2);
        path.Close();
        canvas.DrawPath(path, headPaint);
    }

    private void ApplyMosaic(int startX, int startY, int width, int height)
    {
        if (_currentBitmap == null || width <= 2 || height <= 2) return;

        int rectLeft = Math.Clamp(startX, 0, _currentBitmap.Width);
        int rectTop = Math.Clamp(startY, 0, _currentBitmap.Height);
        int rectRight = Math.Clamp(startX + width, 0, _currentBitmap.Width);
        int rectBottom = Math.Clamp(startY + height, 0, _currentBitmap.Height);

        int blockSize = Math.Max(6, Math.Min(_currentBitmap.Width, _currentBitmap.Height) / 50);

        for (int y = rectTop; y < rectBottom; y += blockSize)
        {
            for (int x = rectLeft; x < rectRight; x += blockSize)
            {
                long sumR = 0, sumG = 0, sumB = 0, count = 0;
                int currentBlockW = Math.Min(blockSize, rectRight - x);
                int currentBlockH = Math.Min(blockSize, rectBottom - y);

                for (int dy = 0; dy < currentBlockH; dy++)
                {
                    for (int dx = 0; dx < currentBlockW; dx++)
                    {
                        var pixel = _currentBitmap.GetPixel(x + dx, y + dy);
                        sumR += pixel.Red;
                        sumG += pixel.Green;
                        sumB += pixel.Blue;
                        count++;
                    }
                }

                if (count > 0)
                {
                    var avgColor = new SKColor(
                        (byte)(sumR / count),
                        (byte)(sumG / count),
                        (byte)(sumB / count));

                    for (int dy = 0; dy < currentBlockH; dy++)
                    {
                        for (int dx = 0; dx < currentBlockW; dx++)
                        {
                            _currentBitmap.SetPixel(x + dx, y + dy, avgColor);
                        }
                    }
                }
            }
        }
    }

    private void SaveImage(bool overwrite)
    {
        if (_currentBitmap == null) return;

        try
        {
            string targetPath;
            if (overwrite)
            {
                targetPath = _sourceFilePath;
            }
            else
            {
                var dir = Path.GetDirectoryName(_sourceFilePath) ?? "";
                var nameWithoutExt = Path.GetFileNameWithoutExtension(_sourceFilePath);
                var ext = Path.GetExtension(_sourceFilePath);
                targetPath = Path.Combine(dir, $"{nameWithoutExt}_edited{ext}");
            }

            using var image = SKImage.FromBitmap(_currentBitmap);
            var format = Path.GetExtension(targetPath).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
                ".webp" => SKEncodedImageFormat.Webp,
                _ => SKEncodedImageFormat.Png
            };
            using var data = image.Encode(format, 100);

            using var fs = File.Create(targetPath);
            data.SaveTo(fs);
            fs.Flush();

            _onSaved(targetPath, overwrite);
            Close();
        }
        catch
        {
            // 忽略写入异常
        }
    }
}
