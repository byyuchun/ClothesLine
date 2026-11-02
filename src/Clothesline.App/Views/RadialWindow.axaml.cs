using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Clothesline.Core.Models;
using Clothesline.Engines;

namespace Clothesline.App.Views;

public partial class RadialWindow : Window
{
    private RopeItem? _currentItem;
    private Action<string>? _onConverted;
    private bool _conversionStarted;

    public RadialWindow()
    {
        InitializeComponent();

        Donut.SliceSelected += async (_, format) => await StartConversionAsync(format);

        // 点击轮盘外部区域自动关闭取消
        PointerPressed += (_, e) =>
        {
            if (!_conversionStarted && Donut.HoveredIndex < 0)
            {
                Close();
            }
        };

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && !_conversionStarted)
            {
                Close();
            }
        };
    }

    public void ShowForConversion(RopeItem item, PixelPoint cursorLocation, Action<string> onConverted)
    {
        _currentItem = item;
        _onConverted = onConverted;
        _conversionStarted = false;

        // 以光标为圆心居中展开
        Position = new PixelPoint(cursorLocation.X - 180, cursorLocation.Y - 180);

        var targetFormats = ConversionService.Current.GetTargetFormats(item.Extension);
        Donut.SetFormats(targetFormats);

        Show();
        Activate();
    }

    private async Task StartConversionAsync(string targetFormat)
    {
        if (_conversionStarted || _currentItem == null) return;
        _conversionStarted = true;

        Donut.StartConverting(targetFormat);

        try
        {
            var progress = new Progress<double>(p =>
            {
                Dispatcher.UIThread.Post(() => Donut.UpdateProgress(p));
            });

            var outputPath = await ConversionService.Current.ConvertAsync(_currentItem.FilePath, targetFormat, progress);

            Donut.MarkSuccess();
            await Task.Delay(400);

            _onConverted?.Invoke(outputPath);
            Close();
        }
        catch
        {
            // 发生异常时重置进度并关闭
            Dispatcher.UIThread.Post(() =>
            {
                Donut.UpdateProgress(0);
            });
            await Task.Delay(600);
            Close();
        }
    }
}
