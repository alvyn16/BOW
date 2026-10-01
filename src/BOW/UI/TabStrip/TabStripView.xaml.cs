using BOW.Core;
using BOW.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
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
    private readonly Button _backButton;
    private readonly Button _forwardButton;
    private readonly Button _reloadButton;
    private readonly Dictionary<Guid, BowTab> _observedTabs = new();

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

        _backButton = CreateNavigationButton("\uE72B", "Back");
        _backButton.Click += (_, _) => App.MainWindow?.GoBack();
        root.Children.Add(_backButton);

        _forwardButton = CreateNavigationButton("\uE72A", "Forward");
        _forwardButton.Click += (_, _) => App.MainWindow?.GoForward();
        root.Children.Add(_forwardButton);

        _reloadButton = CreateNavigationButton("\uE72C", "Reload");
        _reloadButton.Margin = new Thickness(0, 0, 8, 0);
        _reloadButton.Click += (_, _) => App.MainWindow?.ReloadFocusedTab();
        root.Children.Add(_reloadButton);

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

    private static Button CreateNavigationButton(string glyph, string label)
    {
        var button = new Button
        {
            Content = new FontIcon
            {
                FontFamily = new FontFamily("Segoe Fluent Icons"), Glyph = glyph, FontSize = 12
            },
            Width = 24, Height = 24, Padding = new Thickness(0),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(4),
            Foreground = ThemeBrushes.TextBrush, IsEnabled = false
        };
        var transparent = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        button.Resources["ButtonBackground"] = transparent;
        button.Resources["ButtonBackgroundPointerOver"] = ThemeBrushes.SelectedBrush;
        button.Resources["ButtonBackgroundPressed"] = ThemeBrushes.SelectedBrush;
        button.Resources["ButtonBackgroundDisabled"] = transparent;
        button.Resources["ButtonBorderBrush"] = transparent;
        button.Resources["ButtonBorderBrushPointerOver"] = transparent;
        button.Resources["ButtonBorderBrushPressed"] = transparent;
        button.Resources["ButtonBorderBrushDisabled"] = transparent;
        button.Resources["ButtonForeground"] = ThemeBrushes.TextBrush;
        button.Resources["ButtonForegroundPointerOver"] = ThemeBrushes.TextBrush;
        button.Resources["ButtonForegroundPressed"] = ThemeBrushes.TextBrush;
        button.Resources["ButtonForegroundDisabled"] = ThemeBrushes.MutedTextBrush;
        ToolTipService.SetToolTip(button, label);
        AutomationProperties.SetName(button, label);
        return button;
    }

    public void SetNavigationState(bool canGoBack, bool canGoForward, bool canReload)
    {
        _backButton.IsEnabled = canGoBack;
        _forwardButton.IsEnabled = canGoForward;
        _reloadButton.IsEnabled = canReload;
    }

    private void RefreshTabs()
    {
        if (_store is null) return;
        var previousPositions = TabReorderMotion.Capture(TabsRepeater, _tabViews, false);
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
        TabReorderMotion.Animate(TabsRepeater, _tabViews, previousPositions, false);
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
        AutomationProperties.SetAutomationId(view, "Tab-" + tab.Id);
        var gesture = new TabDragGesture(view);
        view.Tapped += (_, _) =>
        {
            if (gesture.WasDragged) return;
            App.MainWindow?.ShowBrowser();
            _store?.SetActiveTab(tab.Id);
        };
        view.DragStarting += (_, args) =>
        {
            args.Data.SetText($"bow-tab:{tab.Id}");
            args.Data.RequestedOperation = DataPackageOperation.Move;
            App.MainWindow?.BeginTabDrag(tab.Id);
        };
        view.DropCompleted += (_, _) =>
        {
            foreach (var tabView in _tabViews.Values)
                tabView.RootGrid.BorderThickness = new Thickness(0);
            App.MainWindow?.EndTabDrag();
        };
        view.ContextRequested += TabItem_ContextRequested;
        view.AllowDrop = true;
        view.DragOver += (_, args) =>
        {
            var sourceId = App.MainWindow?.DraggedTabId;
            if (sourceId is not null && sourceId != tab.Id
                && args.DataView.Contains(StandardDataFormats.Text))
            {
                args.AcceptedOperation = DataPackageOperation.Move;
                if (_store?.ActiveTab?.Id == tab.Id && _store.CanSplitWithTab(sourceId.Value))
                {
                    view.RootGrid.BorderThickness = new Thickness(0);
                    App.MainWindow?.PreviewTabSplit(sourceId.Value);
                }
                else
                {
                    var after = args.GetPosition(view).X > view.ActualWidth / 2;
                    view.RootGrid.BorderBrush = ThemeBrushes.AccentBrush;
                    view.RootGrid.BorderThickness = after
                        ? new Thickness(0, 0, 2, 0) : new Thickness(2, 0, 0, 0);
                }
            }
            args.Handled = true;
        };
        view.DragLeave += (_, _) =>
        {
            view.RootGrid.BorderThickness = new Thickness(0);
            App.MainWindow?.ClearTabSplitPreview();
        };
        view.Drop += (_, args) =>
        {
            view.RootGrid.BorderThickness = new Thickness(0);
            if (_store is not null && App.MainWindow?.DraggedTabId is Guid sourceId
                && sourceId != tab.Id)
            {
                if (_store.ActiveTab?.Id == tab.Id && _store.CanSplitWithTab(sourceId))
                    App.MainWindow.SplitDraggedTab(false);
                else
                    _store.MoveTab(sourceId, tab.Id, args.GetPosition(view).X > view.ActualWidth / 2);
                args.AcceptedOperation = DataPackageOperation.Move;
            }
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

    private void OnTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BowTab.GroupName)) DispatcherQueue.TryEnqueue(RefreshTabs);
    }
}
