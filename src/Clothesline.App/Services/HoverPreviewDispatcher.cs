using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Clothesline.App.Controls;
using Clothesline.App.Views;

namespace Clothesline.App.Services;

/// <summary>
/// 卡片长时间悬浮预览调度器（防抖计时、多模态解析调度与防裁切定位）
/// </summary>
public sealed class HoverPreviewDispatcher : IDisposable
{
    private readonly ShelfWindow _shelf;
    private readonly DispatcherTimer _delayTimer = new();
    private readonly DispatcherTimer _leaveTimer = new() { Interval = TimeSpan.FromMilliseconds(80) };

    private ShotCard? _currentTarget;
    private HoverPreviewWindow? _previewWindow;
    private DateTime _lastShownTime = DateTime.MinValue;

    public HoverPreviewDispatcher(ShelfWindow shelf)
    {
        _shelf = shelf;
        _delayTimer.Tick += OnDelayTimerTick;
        _leaveTimer.Tick += OnLeaveTimerTick;
    }

    public void RegisterCard(ShotCard card)
    {
        card.PointerEntered += (_, _) => OnCardPointerEntered(card);
        card.PointerExited += (_, _) => OnCardPointerExited(card);
    }

    private void OnCardPointerEntered(ShotCard card)
    {
        if (!_shelf.IsRevealed) return;

        // 如果在离开缓冲期内重新进入同一张卡片，取消关闭
        if (_currentTarget == card && (_previewWindow?.IsVisible == true || _delayTimer.IsEnabled))
        {
            _leaveTimer.Stop();
            return;
        }

        _delayTimer.Stop();
        _leaveTimer.Stop();

        _currentTarget = card;
        DismissWindow();

        // 最近 1 秒内刚看过其他预览，则快速切换响应 (150ms)；否则按标准防抖 (400ms)
        bool isQuickSwitch = (DateTime.UtcNow - _lastShownTime).TotalMilliseconds < 1000;
        _delayTimer.Interval = TimeSpan.FromMilliseconds(isQuickSwitch ? 150 : 400);
        _delayTimer.Start();
    }

    private void OnCardPointerExited(ShotCard card)
    {
        if (_currentTarget != card) return;

        _delayTimer.Stop();
        _leaveTimer.Stop();
        _leaveTimer.Start();
    }

    private void OnLeaveTimerTick(object? sender, EventArgs e)
    {
        _leaveTimer.Stop();
        _currentTarget = null;
        DismissWindow();
    }

    private async void OnDelayTimerTick(object? sender, EventArgs e)
    {
        _delayTimer.Stop();

        if (_currentTarget == null || !_shelf.IsRevealed) return;

        var targetCard = _currentTarget;
        var item = targetCard.Item;

        var result = await PreviewGenerator.GenerateAsync(item);

        // 如果在异步生成过程中目标已改变或已移开，则放弃渲染
        if (_currentTarget != targetCard || !_shelf.IsRevealed) return;

        var screen = _shelf.Screens.Primary;
        if (screen == null) return;

        DismissWindow();

        _previewWindow = new HoverPreviewWindow();
        _previewWindow.ApplyPreview(result);

        // 计算卡片底部中点的全局屏幕坐标
        double cardLeft = Canvas.GetLeft(targetCard);
        double cardTop = Canvas.GetTop(targetCard);

        int anchorScreenX = _shelf.Position.X + (int)(cardLeft + targetCard.Bounds.Width / 2.0);
        int anchorScreenY = _shelf.Position.Y + (int)(cardTop + targetCard.Bounds.Height);

        _previewWindow.PositionNearAnchor(new PixelPoint(anchorScreenX, anchorScreenY), screen.WorkingArea);
        _previewWindow.Show();

        _lastShownTime = DateTime.UtcNow;
    }

    public void Dismiss()
    {
        _delayTimer.Stop();
        _leaveTimer.Stop();
        _currentTarget = null;
        DismissWindow();
    }

    private void DismissWindow()
    {
        if (_previewWindow != null)
        {
            _previewWindow.Close();
            _previewWindow = null;
        }
    }

    public void Dispose()
    {
        Dismiss();
    }
}
