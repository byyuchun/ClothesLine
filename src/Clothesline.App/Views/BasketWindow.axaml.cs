using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Clothesline.Core.Models;
using Clothesline.Core.Storage;
using Clothesline.Platform;

namespace Clothesline.App.Views;

public partial class BasketWindow : Window
{
    private readonly IStateStore _stateStore;
    private readonly Action<RopeItem>? _onRehangCallback;
    private readonly Action? _onBasketChanged;
    private List<RopeItem> _items = [];
    private RopeItem? _selectedItem;

    public BasketWindow() : this(new StateStore(), null, null)
    {
    }

    public BasketWindow(IStateStore stateStore, Action<RopeItem>? onRehangCallback, Action? onBasketChanged = null)
    {
        InitializeComponent();
        _stateStore = stateStore;
        _onRehangCallback = onRehangCallback;
        _onBasketChanged = onBasketChanged;

        HeaderBar.PointerPressed += OnHeaderPointerPressed;
        CloseButton.Click += (_, _) => Close();
        BottomCloseButton.Click += (_, _) => Close();

        RehangButton.Click += OnRehangClicked;
        LocateButton.Click += OnLocateClicked;
        OpenButton.Click += OnOpenClicked;
        DeleteFromBasketButton.Click += OnDeleteClicked;
        ClearBasketButton.Click += OnClearClicked;

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        };
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        await RefreshAsync();
    }

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    public async Task RefreshAsync()
    {
        _items = await _stateStore.LoadBasketAsync();
        CountHeader.Text = $"暂存文件 ({_items.Count} 项)";

        ItemsPanel.Children.Clear();

        if (_items.Count == 0)
        {
            EmptyContainer.IsVisible = true;
            ItemsPanel.IsVisible = false;
            _selectedItem = null;
            UpdateDetail(null);
            return;
        }

        EmptyContainer.IsVisible = false;
        ItemsPanel.IsVisible = true;

        foreach (var item in _items)
        {
            var row = CreateItemRow(item);
            ItemsPanel.Children.Add(row);
        }

        // 默认选中第一项或之前选中的项
        var target = _items.FirstOrDefault(x => x.Id == _selectedItem?.Id) ?? _items[0];
        SelectRow(target);
    }

    private Control CreateItemRow(RopeItem item)
    {
        var rowBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#FFFDF9")),
            BorderBrush = new SolidColorBrush(Color.Parse("#1E293B")),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(6),
            BoxShadow = BoxShadows.Parse("2 2 0 0 #1E293B"),
            Padding = new Thickness(12, 8),
            Cursor = new Cursor(StandardCursorType.Hand),
            Tag = item
        };

        var rowGrid = new Grid();
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        // 1. 文件类型徽标
        var badgeBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse(item.Badge.BackgroundColorHex)),
            BorderBrush = new SolidColorBrush(Color.Parse("#1E293B")),
            BorderThickness = new Thickness(1.2),
            CornerRadius = new CornerRadius(4),
            BoxShadow = BoxShadows.Parse("1.5 1.5 0 0 #1E293B"),
            Padding = new Thickness(6, 2),
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        var badgeText = new TextBlock
        {
            Text = item.Badge.Text,
            FontSize = 10,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse(item.Badge.ForegroundColorHex))
        };
        badgeBorder.Child = badgeText;
        Grid.SetColumn(badgeBorder, 0);
        rowGrid.Children.Add(badgeBorder);

        // 2. 文件信息
        var infoStack = new StackPanel
        {
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Spacing = 2
        };
        var nameText = new TextBlock
        {
            Text = item.FileName,
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse("#1E293B")),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var subText = new TextBlock
        {
            Text = $"{item.FormattedFileSize} · 归档于 {item.LastAccessedTime:MM-dd HH:mm}",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#64748B"))
        };
        infoStack.Children.Add(nameText);
        infoStack.Children.Add(subText);
        Grid.SetColumn(infoStack, 1);
        rowGrid.Children.Add(infoStack);

        // 3. 快速挂绳按钮
        var hangBtn = new Button
        {
            Content = "挂绳",
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            Height = 26,
            Padding = new Thickness(10, 0),
            Background = new SolidColorBrush(Color.Parse("#1E293B")),
            Foreground = new SolidColorBrush(Color.Parse("#FFFDF9")),
            BorderBrush = new SolidColorBrush(Color.Parse("#1E293B")),
            BorderThickness = new Thickness(1.2),
            CornerRadius = new CornerRadius(4),
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        hangBtn.Click += async (_, e) =>
        {
            e.Handled = true;
            await RehangItemAsync(item);
        };
        Grid.SetColumn(hangBtn, 2);
        rowGrid.Children.Add(hangBtn);

        rowBorder.Child = rowGrid;

        rowBorder.PointerEntered += (_, _) =>
        {
            if (_selectedItem?.Id != item.Id)
            {
                rowBorder.Background = new SolidColorBrush(Color.Parse("#FBF7F0"));
            }
        };

        rowBorder.PointerExited += (_, _) =>
        {
            if (_selectedItem?.Id != item.Id)
            {
                rowBorder.Background = new SolidColorBrush(Color.Parse("#FFFDF9"));
            }
        };

        rowBorder.PointerPressed += (_, _) =>
        {
            SelectRow(item);
        };

        return rowBorder;
    }

    private void SelectRow(RopeItem item)
    {
        _selectedItem = item;

        // 高亮行
        foreach (var child in ItemsPanel.Children)
        {
            if (child is Border b && b.Tag is RopeItem rowItem)
            {
                bool isSelected = rowItem.Id == item.Id;
                b.Background = isSelected
                    ? new SolidColorBrush(Color.Parse("#F0E8DC"))
                    : new SolidColorBrush(Color.Parse("#FFFDF9"));
                b.BorderThickness = isSelected ? new Thickness(2.0) : new Thickness(1.5);
                b.BoxShadow = isSelected
                    ? BoxShadows.Parse("2.5 2.5 0 0 #1E293B")
                    : BoxShadows.Parse("2 2 0 0 #1E293B");
            }
        }

        UpdateDetail(item);
    }

    private void UpdateDetail(RopeItem? item)
    {
        if (item == null)
        {
            DetailFileName.Text = "未选择文件";
            DetailFileSize.Text = "-";
            DetailFilePath.Text = "-";
            PreviewImage.IsVisible = false;
            PreviewIcon.Text = "文件";
            PreviewIcon.IsVisible = true;
            RehangButton.IsEnabled = false;
            LocateButton.IsEnabled = false;
            OpenButton.IsEnabled = false;
            DeleteFromBasketButton.IsEnabled = false;
            return;
        }

        DetailFileName.Text = item.FileName;
        DetailFileSize.Text = $"{item.FormattedFileSize} · {item.Badge.Text}";
        DetailFilePath.Text = item.FilePath;

        RehangButton.IsEnabled = true;
        LocateButton.IsEnabled = true;
        OpenButton.IsEnabled = true;
        DeleteFromBasketButton.IsEnabled = true;

        // 预览图片
        if (item.Kind == ItemKind.Image && File.Exists(item.FilePath))
        {
            try
            {
                var bmp = new Bitmap(item.FilePath);
                PreviewImage.Source = bmp;
                PreviewImage.IsVisible = true;
                PreviewIcon.IsVisible = false;
            }
            catch
            {
                PreviewImage.IsVisible = false;
                PreviewIcon.Text = "图片";
                PreviewIcon.IsVisible = true;
            }
        }
        else
        {
            PreviewImage.IsVisible = false;
            PreviewIcon.IsVisible = true;
            PreviewIcon.Text = item.Badge.Text;
        }
    }

    private async Task RehangItemAsync(RopeItem item)
    {
        _onRehangCallback?.Invoke(item);
        await _stateStore.RemoveFromBasketAsync(item.Id);
        _onBasketChanged?.Invoke();
        await RefreshAsync();
    }

    private async void OnRehangClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_selectedItem != null)
        {
            await RehangItemAsync(_selectedItem);
        }
    }

    private void OnLocateClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_selectedItem != null)
        {
            PlatformProvider.Current.LocateInFileManager(_selectedItem.FilePath);
        }
    }

    private void OnOpenClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_selectedItem != null)
        {
            PlatformProvider.Current.OpenFile(_selectedItem.FilePath);
        }
    }

    private async void OnDeleteClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_selectedItem != null)
        {
            await _stateStore.RemoveFromBasketAsync(_selectedItem.Id);
            _onBasketChanged?.Invoke();
            await RefreshAsync();
        }
    }

    private async void OnClearClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await _stateStore.ClearBasketAsync();
        _onBasketChanged?.Invoke();
        await RefreshAsync();
    }
}
