using BOW.Core;
using BOW.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using System.ComponentModel;

namespace BOW.UI.TabStrip;

public class TabItemViewModel
{
    public BowTab Tab { get; }
    public TabItemViewModel(BowTab tab) => Tab = tab;
}

public sealed class TabStripView : UserControl
{
    private BowStore? _store;
    private readonly Dictionary<Guid, TabItemView> _tabViews = new();
    private readonly Dictionary<Guid, Button> _groupHeadings = new();
    private readonly ScrollViewer _scrollView;
    private readonly Button _addButton;
    private readonly Button _moreButton;
    private readonly Image _appIconImage;
    private readonly Dictionary<Guid, BowTab> _observedTabs = new();
    private Guid? _pointerDragTabId;
    private double _pointerDragStartX;

    public StackPanel TabsRepeater { get; }
    public StackPanel InteractiveContent { get; }

    public TabStripView()
    {
        var root = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left,
            Spacing = 4
        };
        InteractiveContent = root;

        _appIconImage = new Image
        {
            Width = 20, Height = 20,
            Source = AppIconAssets.Preview("Black")
        };
        var sidebarToggleBtn = new Button
        {
            Content = _appIconImage,
            Width = 28, Height = 28, Padding = new Thickness(0),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 0, 4, 0)
        };
        ToolTipService.SetToolTip(sidebarToggleBtn, "Toggle sidebar");
        sidebarToggleBtn.Click += SidebarToggle_Click;
        root.Children.Add(sidebarToggleBtn);

        _scrollView = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        TabsRepeater = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4
        };
        TabsRepeater.AddHandler(UIElement.PointerReleasedEvent,
            new PointerEventHandler(OnTabPointerReleased), true);
        _scrollView.Content = TabsRepeater;
        root.Children.Add(_scrollView);

        _addButton = new Button
        {
            Content = new FontIcon { FontFamily = new FontFamily("Segoe Fluent Icons"), Glyph = "\uE710", FontSize = 12 },
            Width = 28, Height = 28, Padding = new Thickness(0),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(6)
        };
        _addButton.Click += AddTabButton_Click;
        root.Children.Add(_addButton);

        _moreButton = new Button
        {
            Content = "⋯", Width = 28, Height = 28, Padding = new Thickness(0),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(6)
        };
        ToolTipService.SetToolTip(_moreButton, "Tab menu");
        _moreButton.Click += (_, _) =>
        {
            if (_store is not null) TabMenuBuilder.CreateTabsMenu(_store).ShowAt(_moreButton);
        };
        root.Children.Add(_moreButton);

        this.Content = root;
    }

    private void RefreshTabs()
    {
        if (_store is null) return;
        var desired = new List<UIElement>();
        var tabIds = new HashSet<Guid>();
        var headingIds = new HashSet<Guid>();
        string? previousGroup = null;
        foreach (var tab in _store.Tabs)
        {
            tabIds.Add(tab.Id);
            if (tab.GroupName is { } group
                && !string.Equals(group, previousGroup, StringComparison.OrdinalIgnoreCase))
            {
                headingIds.Add(tab.Id);
                desired.Add(GetOrCreateGroupHeading(tab.Id, group));
            }
            previousGroup = tab.GroupName;
            if (!_tabViews.TryGetValue(tab.Id, out var view))
                _tabViews[tab.Id] = view = CreateTabView(tab);
            desired.Add(view);
        }

        var keep = new HashSet<UIElement>(desired);
        for (var i = TabsRepeater.Children.Count - 1; i >= 0; i--)
            if (!keep.Contains(TabsRepeater.Children[i]))
                TabsRepeater.Children.RemoveAt(i);
        foreach (var id in _tabViews.Keys.Where(id => !tabIds.Contains(id)).ToArray())
        {
            _tabViews[id].DataContext = null;
            _tabViews.Remove(id);
        }
        foreach (var id in _groupHeadings.Keys.Where(id => !headingIds.Contains(id)).ToArray())
            _groupHeadings.Remove(id);

        for (var i = 0; i < desired.Count; i++)
        {
            if (i < TabsRepeater.Children.Count && TabsRepeater.Children[i] == desired[i]) continue;
            var oldIndex = TabsRepeater.Children.IndexOf(desired[i]);
            if (oldIndex >= 0) TabsRepeater.Children.RemoveAt(oldIndex);
            TabsRepeater.Children.Insert(i, desired[i]);
        }
    }

    private Button GetOrCreateGroupHeading(Guid id, string group)
    {
        if (!_groupHeadings.TryGetValue(id, out var heading))
        {
            heading = new Button
            {
                Height = 28, MaxWidth = 104,
                Padding = new Thickness(7, 0, 7, 0),
                FontFamily = ThemeBrushes.UiFont, FontSize = 10,
                Background = ThemeBrushes.SelectedBrush,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(6)
            };
            heading.ContextRequested += (_, _) =>
            {
                if (_store is not null && heading.Tag is string name)
                    TabMenuBuilder.CreateGroupMenu(_store, name).ShowAt(heading);
            };
            _groupHeadings[id] = heading;
        }
        heading.Content = group;
        heading.Tag = group;
        ToolTipService.SetToolTip(heading, $"Group: {group}. Right-click to rename or remove.");
        return heading;
    }

    private TabItemView CreateTabView(BowTab tab)
    {
        var view = new TabItemView { DataContext = new TabItemViewModel(tab) };
        view.PointerPressed += TabItem_PointerPressed;
        view.AddHandler(UIElement.PointerPressedEvent,
            new PointerEventHandler((_, args) =>
            {
                var point = args.GetCurrentPoint(TabsRepeater);
                if (!point.Properties.IsLeftButtonPressed) return;
                _pointerDragTabId = tab.Id;
                _pointerDragStartX = point.Position.X;
            }), true);
        view.ContextRequested += TabItem_ContextRequested;
        view.AllowDrop = true;
        view.DragOver += (_, args) =>
        {
            if (args.DataView.Contains(StandardDataFormats.Text))
                args.AcceptedOperation = DataPackageOperation.Move;
            args.Handled = true;
        };
        view.Drop += async (_, args) =>
        {
            if (_store is null || !args.DataView.Contains(StandardDataFormats.Text)) return;
            var value = await args.DataView.GetTextAsync();
            if (value.StartsWith("bow-tab:", StringComparison.Ordinal)
                && Guid.TryParse(value[8..], out var sourceId))
                _store.MoveTab(sourceId, tab.Id, args.GetPosition(view).X > view.ActualWidth / 2);
            args.Handled = true;
        };
        return view;
    }

    public void Initialize(BowStore store)
    {
        _store = store;
        SyncAppIcon(store.Settings.AppIconVariant);

        SyncGroupSubscriptions();
        RefreshTabs();

        store.Tabs.CollectionChanged += (_, _) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                SyncGroupSubscriptions();
                RefreshTabs();
            });
        };
    }

    public void FocusOmnibar() => App.MainWindow?.FocusOmnibar();

    public void SyncAppIcon(string variant) => _appIconImage.Source = AppIconAssets.Preview(variant);

    public void SetLayout(bool sidebar)
    {
        _scrollView.Visibility = sidebar ? Visibility.Collapsed : Visibility.Visible;
        _addButton.Visibility = sidebar ? Visibility.Collapsed : Visibility.Visible;
        _moreButton.Visibility = sidebar ? Visibility.Collapsed : Visibility.Visible;
    }

    private void AddTabButton_Click(object sender, RoutedEventArgs e) => _store?.AddTab("bow:newtab");

    private void SidebarToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_store is null) return;
        _store.Settings.TabLayout = _store.Settings.TabLayout == "Sidebar" ? "Strip" : "Sidebar";
        SettingsService.Save(_store.Settings);
        App.MainWindow?.ApplyTabLayout();
    }

    internal void TabItem_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is TabItemView { DataContext: TabItemViewModel vm } && _store is not null)
        {
            App.MainWindow?.ShowBrowser();
            _store.SetActiveTab(vm.Tab.Id);
        }
    }

    internal void TabItem_ContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        if (sender is TabItemView { DataContext: TabItemViewModel vm } && _store is not null)
            ShowTabContextMenu(sender, vm.Tab);
    }

    private void ShowTabContextMenu(UIElement sender, BowTab tab)
    {
        TabMenuBuilder.CreateTabMenu(_store!, tab).ShowAt(sender, new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions
        {
            Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedLeft
        });
    }

    private void SyncGroupSubscriptions()
    {
        if (_store is null) return;
        foreach (var old in _observedTabs.Values.Where(tab => !_store.Tabs.Contains(tab)).ToArray())
        {
            old.PropertyChanged -= OnTabPropertyChanged;
            _observedTabs.Remove(old.Id);
        }
        foreach (var tab in _store.Tabs.Where(tab => !_observedTabs.ContainsKey(tab.Id)))
        {
            tab.PropertyChanged += OnTabPropertyChanged;
            _observedTabs[tab.Id] = tab;
        }
    }

    private void OnTabPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var sourceId = _pointerDragTabId;
        _pointerDragTabId = null;
        if (sourceId is null || _store is null) return;
        var point = e.GetCurrentPoint(TabsRepeater).Position;
        if (Math.Abs(point.X - _pointerDragStartX) <= 7) return;
        foreach (var target in TabsRepeater.Children.OfType<TabItemView>())
        {
            if (target.DataContext is not TabItemViewModel vm) continue;
            var left = target.TransformToVisual(TabsRepeater)
                .TransformPoint(new Windows.Foundation.Point(0, 0)).X;
            if (point.X < left || point.X > left + target.ActualWidth) continue;
            _store.MoveTab(sourceId.Value, vm.Tab.Id,
                point.X > left + target.ActualWidth / 2);
            break;
        }
    }

    private void OnTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BowTab.GroupName)) DispatcherQueue.TryEnqueue(RefreshTabs);
    }
}
