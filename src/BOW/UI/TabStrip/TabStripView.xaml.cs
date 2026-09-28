using BOW.Core;
using BOW.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using System.Collections.ObjectModel;
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
    private readonly ObservableCollection<TabItemViewModel> _items = new();
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

    private object CreateTemplate()
    {
        return new TabElementFactory();
    }

    private class TabElementFactory : Microsoft.UI.Xaml.IElementFactory
    {
        public Microsoft.UI.Xaml.UIElement GetElement(Microsoft.UI.Xaml.ElementFactoryGetArgs args)
        {
            var view = new TabItemView();
            view.DataContext = args.Data;
            return view;
        }

        public void RecycleElement(Microsoft.UI.Xaml.ElementFactoryRecycleArgs args)
        {
            if (args.Element is Microsoft.UI.Xaml.FrameworkElement fw)
                fw.DataContext = null;
        }
    }

    private void RefreshTabs()
    {
        foreach (var old in TabsRepeater.Children.OfType<TabItemView>()) old.DataContext = null;
        TabsRepeater.Children.Clear();
        string? previousGroup = null;
        foreach (var vm in _items)
        {
            if (vm.Tab.GroupName is { } group
                && !string.Equals(group, previousGroup, StringComparison.OrdinalIgnoreCase))
            {
                var heading = new Button
                {
                    Content = group, Height = 28, MaxWidth = 104,
                    Padding = new Thickness(7, 0, 7, 0),
                    FontFamily = ThemeBrushes.UiFont, FontSize = 10,
                    Background = ThemeBrushes.SelectedBrush,
                    BorderThickness = new Thickness(0),
                    CornerRadius = new CornerRadius(6)
                };
                ToolTipService.SetToolTip(heading, $"Group: {group}. Right-click to rename or remove.");
                heading.ContextRequested += (_, _) =>
                    TabMenuBuilder.CreateGroupMenu(_store!, group).ShowAt(heading);
                TabsRepeater.Children.Add(heading);
            }
            previousGroup = vm.Tab.GroupName;
            var el = (TabItemView)new TabElementFactory().GetElement(new Microsoft.UI.Xaml.ElementFactoryGetArgs { Data = vm });
            el.PointerPressed += TabItem_PointerPressed;
            el.AddHandler(UIElement.PointerPressedEvent,
                new PointerEventHandler((_, args) =>
                {
                    var point = args.GetCurrentPoint(TabsRepeater);
                    if (!point.Properties.IsLeftButtonPressed) return;
                    _pointerDragTabId = vm.Tab.Id;
                    _pointerDragStartX = point.Position.X;
                }), true);
            el.ContextRequested += TabItem_ContextRequested;
            el.AllowDrop = true;
            el.DragOver += (_, args) =>
            {
                if (args.DataView.Contains(StandardDataFormats.Text))
                    args.AcceptedOperation = DataPackageOperation.Move;
                args.Handled = true;
            };
            el.Drop += async (_, args) =>
            {
                if (_store is null || !args.DataView.Contains(StandardDataFormats.Text)) return;
                var value = await args.DataView.GetTextAsync();
                if (value.StartsWith("bow-tab:", StringComparison.Ordinal)
                    && Guid.TryParse(value[8..], out var sourceId))
                    _store.MoveTab(sourceId, vm.Tab.Id, args.GetPosition(el).X > el.ActualWidth / 2);
                args.Handled = true;
            };
            TabsRepeater.Children.Add(el);
        }
    }

    public void Initialize(BowStore store)
    {
        _store = store;
        SyncAppIcon(store.Settings.AppIconVariant);

        foreach (var tab in store.Tabs)
            _items.Add(new TabItemViewModel(tab));
        SyncGroupSubscriptions();
        RefreshTabs();

        store.Tabs.CollectionChanged += (_, _) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _items.Clear();
                foreach (var tab in store.Tabs)
                    _items.Add(new TabItemViewModel(tab));
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
