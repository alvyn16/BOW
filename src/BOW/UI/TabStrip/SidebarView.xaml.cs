using BOW.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.ComponentModel;
using Windows.ApplicationModel.DataTransfer;

namespace BOW.UI.TabStrip;

public sealed class SidebarView : UserControl
{
    private BowStore? _store;
    private readonly List<(BowTab Tab, PropertyChangedEventHandler Handler)> _tabHandlers = new();
    private readonly Dictionary<Guid, Border> _tabRows = new();
    private Guid? _pointerDragTabId;
    private Windows.Foundation.Point _pointerDragStart;
    private bool _pointerDragStarted;

    public StackPanel TabList { get; }

    public SidebarView()
    {
        var root = new Grid { Background = ThemeBrushes.SidebarBrush };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new Grid { Margin = new Thickness(16, 13, 12, 8) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock
        {
            Text = "TABS",
            FontFamily = ThemeBrushes.UiFont,
            FontSize = 10,
            CharacterSpacing = 70,
            Foreground = ThemeBrushes.MutedTextBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        var addButton = new Button
        {
            Content = new FontIcon { FontFamily = new FontFamily("Segoe Fluent Icons"), Glyph = "\uE710", FontSize = 12 },
            Width = 26,
            Height = 26,
            Padding = new Thickness(0),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Foreground = ThemeBrushes.TextBrush,
            CornerRadius = new CornerRadius(6)
        };
        ToolTipService.SetToolTip(addButton, "New tab");
        addButton.Click += (_, _) => _store?.AddTab("bow:newtab");
        Grid.SetColumn(addButton, 1);
        header.Children.Add(addButton);
        var moreButton = new Button
        {
            Content = "⋯", Width = 26, Height = 26, Padding = new Thickness(0),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(6)
        };
        ToolTipService.SetToolTip(moreButton, "Tab menu");
        moreButton.Click += (_, _) =>
        {
            if (_store is not null) TabMenuBuilder.CreateTabsMenu(_store).ShowAt(moreButton);
        };
        Grid.SetColumn(moreButton, 2);
        header.Children.Add(moreButton);
        root.Children.Add(header);

        TabList = new StackPanel { Spacing = 2, Padding = new Thickness(8, 0, 8, 8) };
        TabList.AddHandler(UIElement.PointerMovedEvent,
            new PointerEventHandler(OnTabPointerMoved), true);
        TabList.AddHandler(UIElement.PointerReleasedEvent,
            new PointerEventHandler(OnTabPointerReleased), true);
        var scrollView = new ScrollViewer
        {
            Content = TabList,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetRow(scrollView, 1);
        root.Children.Add(scrollView);
        root.Children.Add(new Border
        {
            Width = 1,
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = ThemeBrushes.TopBarBorderBrush
        });
        Content = root;
    }

    public void Initialize(BowStore store)
    {
        _store = store;
        Refresh();
        store.Tabs.CollectionChanged += (_, _) => DispatcherQueue.TryEnqueue(Refresh);
        store.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BowStore.ActiveTab))
                DispatcherQueue.TryEnqueue(RefreshActiveSelection);
        };
    }

    public void Refresh()
    {
        if (_store is null) return;
        foreach (var (tab, handler) in _tabHandlers) tab.PropertyChanged -= handler;
        _tabHandlers.Clear();
        _tabRows.Clear();
        TabList.Children.Clear();

        string? previousGroup = null;
        foreach (var tab in _store.Tabs)
        {
            if (tab.GroupName is { } group
                && !string.Equals(group, previousGroup, StringComparison.OrdinalIgnoreCase))
            {
                var heading = new Button
                {
                    Content = group, Height = 25,
                    Padding = new Thickness(9, 0, 6, 0),
                    Margin = new Thickness(0, 7, 0, 1),
                    FontFamily = ThemeBrushes.UiFont, FontSize = 10,
                    Foreground = ThemeBrushes.MutedTextBrush,
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    BorderThickness = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left
                };
                ToolTipService.SetToolTip(heading, $"Group: {group}. Right-click to rename or remove.");
                heading.ContextRequested += (_, _) =>
                    TabMenuBuilder.CreateGroupMenu(_store, group).ShowAt(heading);
                heading.AllowDrop = true;
                heading.DragOver += (_, args) =>
                {
                    if (args.DataView.Contains(StandardDataFormats.Text))
                        args.AcceptedOperation = DataPackageOperation.Move;
                    args.Handled = true;
                };
                heading.Drop += async (_, args) =>
                {
                    if (!args.DataView.Contains(StandardDataFormats.Text)) return;
                    var value = await args.DataView.GetTextAsync();
                    if (value.StartsWith("bow-tab:", StringComparison.Ordinal)
                        && Guid.TryParse(value[8..], out var sourceId))
                        _store.SetTabGroup(sourceId, group);
                    args.Handled = true;
                };
                TabList.Children.Add(heading);
            }
            previousGroup = tab.GroupName;
            var row = new Border { CornerRadius = new CornerRadius(7) };
            var layout = new Grid { Height = 36 };
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = new Image { Width = 15, Height = 15, VerticalAlignment = VerticalAlignment.Center };
            var fallback = new FontIcon
            {
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                Glyph = "\uE774",
                FontSize = 13,
                Foreground = ThemeBrushes.MutedTextBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            var iconLayer = new Grid { Width = 18, Height = 18 };
            iconLayer.Children.Add(fallback);
            iconLayer.Children.Add(icon);

            var title = new TextBlock
            {
                FontFamily = ThemeBrushes.UiFont,
                FontSize = 13,
                Foreground = ThemeBrushes.TextBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            var label = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            label.Children.Add(iconLayer);
            label.Children.Add(title);
            var muteIcon = new FontIcon
            {
                FontFamily = new FontFamily("Segoe Fluent Icons"), Glyph = "\uE74F",
                FontSize = 10, Foreground = ThemeBrushes.MutedTextBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            label.Children.Add(muteIcon);
            var transparent = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            var selectButton = new Button
            {
                Content = label,
                Background = transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(9, 0, 2, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(7)
            };
            // The row owns the hover state; the default Button hover creates an inset pill.
            selectButton.Resources["ButtonBackgroundPointerOver"] = transparent;
            selectButton.Resources["ButtonBackgroundPressed"] = transparent;
            selectButton.Click += (_, _) =>
            {
                App.MainWindow?.ShowBrowser();
                _store.SetActiveTab(tab.Id);
            };
            selectButton.ContextRequested += (_, _) =>
                TabMenuBuilder.CreateTabMenu(_store, tab).ShowAt(selectButton,
                    new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions
                    {
                        Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.Right
                    });
            selectButton.AddHandler(UIElement.PointerPressedEvent,
                new PointerEventHandler((_, args) =>
                {
                    var point = args.GetCurrentPoint(TabList);
                    if (!point.Properties.IsLeftButtonPressed) return;
                    _pointerDragTabId = tab.Id;
                    _pointerDragStart = point.Position;
                    _pointerDragStarted = false;
                }), true);
            selectButton.AllowDrop = true;
            selectButton.DragOver += (_, args) =>
            {
                if (args.DataView.Contains(StandardDataFormats.Text))
                    args.AcceptedOperation = DataPackageOperation.Move;
                args.Handled = true;
            };
            selectButton.Drop += async (_, args) =>
            {
                if (!args.DataView.Contains(StandardDataFormats.Text)) return;
                var value = await args.DataView.GetTextAsync();
                if (value.StartsWith("bow-tab:", StringComparison.Ordinal)
                    && Guid.TryParse(value[8..], out var sourceId))
                    _store.MoveTab(sourceId, tab.Id,
                        args.GetPosition(selectButton).Y > selectButton.ActualHeight / 2);
                args.Handled = true;
            };
            layout.Children.Add(selectButton);

            var closeButton = new Button
            {
                Content = "×",
                FontSize = 15,
                Width = 28,
                Height = 28,
                Padding = new Thickness(0),
                Foreground = ThemeBrushes.MutedTextBrush,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(5)
            };
            ToolTipService.SetToolTip(closeButton, "Close tab");
            closeButton.Click += (_, _) => _store.CloseTab(tab.Id);
            Grid.SetColumn(closeButton, 1);
            layout.Children.Add(closeButton);
            row.Child = layout;
            ToolTipService.SetToolTip(row, tab.Title);
            row.PointerEntered += (_, _) => row.Background = ThemeBrushes.SelectedBrush;
            row.PointerExited += (_, _) => row.Background = tab.Id == _store?.ActiveTab?.Id
                ? ThemeBrushes.SelectedBrush : transparent;

            void UpdateTab()
            {
                title.Text = tab.Title;
                muteIcon.Visibility = tab.IsMuted ? Visibility.Visible : Visibility.Collapsed;
                ToolTipService.SetToolTip(row, tab.IsSleeping ? $"{tab.Title} · Sleeping" : tab.Title);
                if (tab.IsSleeping)
                {
                    icon.Source = null;
                    icon.Visibility = Visibility.Collapsed;
                    fallback.Glyph = "\uE708";
                    fallback.Visibility = Visibility.Visible;
                }
                else if (Uri.TryCreate(tab.FaviconUrl, UriKind.Absolute, out var uri))
                {
                    icon.Source = new BitmapImage(uri);
                    icon.Visibility = Visibility.Visible;
                    fallback.Visibility = Visibility.Collapsed;
                }
                else
                {
                    icon.Source = null;
                    icon.Visibility = Visibility.Collapsed;
                    fallback.Glyph = "\uE774";
                    fallback.Visibility = Visibility.Visible;
                }
            }
            PropertyChangedEventHandler handler = (_, e) =>
            {
                if (e.PropertyName == nameof(BowTab.GroupName))
                    DispatcherQueue.TryEnqueue(Refresh);
                else if (e.PropertyName is nameof(BowTab.Title) or nameof(BowTab.FaviconUrl)
                    or nameof(BowTab.IsMuted) or nameof(BowTab.IsSleeping))
                    DispatcherQueue.TryEnqueue(UpdateTab);
            };
            tab.PropertyChanged += handler;
            _tabHandlers.Add((tab, handler));
            UpdateTab();
            _tabRows[tab.Id] = row;
            TabList.Children.Add(row);
        }
        RefreshActiveSelection();
    }

    private void RefreshActiveSelection()
    {
        foreach (var (id, row) in _tabRows)
            row.Background = id == _store?.ActiveTab?.Id
                ? ThemeBrushes.SelectedBrush
                : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    private void OnTabPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_pointerDragTabId is not Guid sourceId) return;
        var point = e.GetCurrentPoint(TabList);
        if (!point.Properties.IsLeftButtonPressed)
        {
            ResetPointerDrag();
            return;
        }
        if (!_pointerDragStarted && Math.Abs(point.Position.Y - _pointerDragStart.Y) > 7)
        {
            _pointerDragStarted = true;
            if (_tabRows.TryGetValue(sourceId, out var row)) row.Opacity = 0.55;
        }
    }

    private void OnTabPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var sourceId = _pointerDragTabId;
        var point = e.GetCurrentPoint(TabList).Position;
        var wasDragging = _pointerDragStarted
            || Math.Abs(point.Y - _pointerDragStart.Y) > 7;
        ResetPointerDrag();
        if (!wasDragging || sourceId is null || _store is null) return;
        foreach (var (targetId, row) in _tabRows)
        {
            var top = row.TransformToVisual(TabList)
                .TransformPoint(new Windows.Foundation.Point(0, 0)).Y;
            if (point.Y < top || point.Y > top + row.ActualHeight) continue;
            _store.MoveTab(sourceId.Value, targetId, point.Y > top + row.ActualHeight / 2);
            break;
        }
    }

    private void ResetPointerDrag()
    {
        if (_pointerDragTabId is Guid sourceId && _tabRows.TryGetValue(sourceId, out var row))
            row.Opacity = 1;
        _pointerDragTabId = null;
        _pointerDragStarted = false;
    }
}
