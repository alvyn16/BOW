using BOW.Core;
using BOW.UI.TabStrip;
using BOW.UI.TopBarView;
using BOW.UI.Settings;
using BOW.UI.WebView;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.System;
using System.Collections.Specialized;
using System.ComponentModel;
using BOW.Services;
using BOW.UI.Security;

namespace BOW;

public sealed class MainWindow : Window
{
    private BowStore Store => App.Store;
    private AppWindow? _appWindow;

    public Grid RootGrid { get; }
    public RowDefinition TopBarRow { get; }
    public TopBarView TopBar { get; }
    public Grid MainSplitView { get; }
    public SidebarView SidebarView { get; }
    private readonly Grid _contentGrid;
    private readonly Grid _contentLayer;
    private readonly Border _contentFrame;
    private readonly Grid _addressOverlay;
    private readonly TextBox _addressBox;
    private readonly StackPanel _recentPages;
    private readonly Button _zenExitButton;
    private AppWindowPresenter? _presenterBeforeZen;
    private bool _isZenMode;
    private bool _windowActivated;
    private SettingsView? _settingsView;
    private readonly Border _splitDivider;
    private readonly Dictionary<Guid, WebViewHost> _tabHosts = new();
    private readonly List<KeyboardAccelerator> _shortcutAccelerators = new();
    private readonly TabSleepService _sleepService;
    public NewTabPage NewTabPageView { get; }
    public TabSwitcherView TabSwitcherView { get; }

    public MainWindow()
    {
        Title = "BOW";

        // Construct UI
        RootGrid = new Grid
        {
            Background = UI.ThemeBrushes.TopBarBrush,
            KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden
        };
        RootGrid.RowDefinitions.Add(TopBarRow = new RowDefinition { Height = new GridLength(40) });
        RootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        TopBar = new TopBarView();
        TopBar.Loaded += (_, _) => UpdateTitleBarHitRegions();
        TopBar.SizeChanged += (_, _) => UpdateTitleBarHitRegions();
        TopBar.TabStrip.InteractiveContent.SizeChanged += (_, _) => UpdateTitleBarHitRegions();
        TopBar.RightControls.SizeChanged += (_, _) => UpdateTitleBarHitRegions();
        Grid.SetRow(TopBar, 0);
        RootGrid.Children.Add(TopBar);

        MainSplitView = new Grid();
        MainSplitView.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Store.Settings.TabLayout == "Sidebar" ? 220 : 0) });
        MainSplitView.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Content
        _contentLayer = new Grid();
        _contentLayer.Children.Add(MainSplitView);
        _contentFrame = new Border
        {
            Background = UI.ThemeBrushes.WindowBackgroundBrush,
            BorderBrush = UI.ThemeBrushes.TopBarBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Margin = new Thickness(8, 0, 8, 8),
            Child = _contentLayer
        };
        Grid.SetRow(_contentFrame, 1);

        SidebarView = new SidebarView();
        SidebarView.Visibility = Store.Settings.TabLayout == "Sidebar" ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumn(SidebarView, 0);
        MainSplitView.Children.Add(SidebarView);

        _contentGrid = new Grid();
        NewTabPageView = new NewTabPage();

        _splitDivider = new Border { Width = 1, Background = UI.ThemeBrushes.TopBarBorderBrush, Visibility = Visibility.Collapsed };
        _contentGrid.Children.Add(_splitDivider);
        _contentGrid.Children.Add(NewTabPageView);

        Grid.SetColumn(_contentGrid, 1);
        MainSplitView.Children.Add(_contentGrid);
        RootGrid.Children.Add(_contentFrame);

        TabSwitcherView = new TabSwitcherView { Visibility = Visibility.Collapsed };
        Grid.SetRow(TabSwitcherView, 1);
        RootGrid.Children.Add(TabSwitcherView);

        _zenExitButton = new Button
        {
            Content = "Exit Zen",
            FontFamily = UI.ThemeBrushes.UiFont,
            FontSize = 11,
            Height = 30,
            Padding = new Thickness(12, 0, 12, 0),
            Margin = new Thickness(0, 10, 10, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Background = UI.ThemeBrushes.ControlSurfaceBrush,
            Foreground = UI.ThemeBrushes.TextBrush,
            BorderBrush = UI.ThemeBrushes.TopBarBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Opacity = 0.88,
            Visibility = Visibility.Collapsed
        };
        _zenExitButton.Click += (_, _) => SetZenMode(false);
        Grid.SetRow(_zenExitButton, 1);
        RootGrid.Children.Add(_zenExitButton);

        _addressOverlay = new Grid
        {
            Background = new SolidColorBrush(Colors.Transparent),
            Visibility = Visibility.Collapsed
        };
        _addressOverlay.PointerPressed += (_, _) => HideAddressOverlay();
        Grid.SetRowSpan(_addressOverlay, 2);
        var addressFrame = new Border
        {
            MaxWidth = 740,
            Margin = new Thickness(24, 56, 24, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Background = UI.ThemeBrushes.ControlSurfaceBrush,
            BorderBrush = UI.ThemeBrushes.TopBarBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8)
        };
        addressFrame.PointerPressed += (_, e) => e.Handled = true;
        var addressPanel = new StackPanel();
        var searchRow = new Grid { Height = 54 };
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        searchRow.Children.Add(new FontIcon
        {
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            Glyph = "\uE721",
            FontSize = 16,
            Foreground = UI.ThemeBrushes.MutedTextBrush,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        _addressBox = new TextBox
        {
            PlaceholderText = "Search with DuckDuckGo or enter address",
            Height = 48,
            FontSize = 13,
            FontFamily = UI.ThemeBrushes.UiFont,
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0, 16, 0, 0),
            BorderThickness = new Thickness(0),
            Background = UI.ThemeBrushes.ControlSurfaceBrush,
            Margin = new Thickness(0, 0, 12, 0)
        };
        UI.ThemeBrushes.StyleTextBox(_addressBox);
        AutomationProperties.SetName(_addressBox, "Search or enter URL");
        _addressBox.KeyDown += AddressBox_KeyDown;
        _addressBox.TextChanged += (_, _) => RefreshRecentPages();
        Grid.SetColumn(_addressBox, 1);
        searchRow.Children.Add(_addressBox);
        addressPanel.Children.Add(searchRow);
        _recentPages = new StackPanel { Padding = new Thickness(4, 4, 4, 8) };
        _recentPages.SizeChanged += (_, _) =>
        {
            foreach (var child in _recentPages.Children.OfType<Button>())
                child.Width = Math.Max(0, _recentPages.ActualWidth - 8);
        };
        addressPanel.Children.Add(_recentPages);
        addressFrame.Child = addressPanel;
        _addressOverlay.Children.Add(addressFrame);
        RootGrid.Children.Add(_addressOverlay);

        this.Content = RootGrid;

        SetupWindowAppearance();
        Activated += (_, _) =>
        {
            if (_windowActivated) return;
            _windowActivated = true;
            ApplyZenPresenter();
        };
        WireStore();
        _sleepService = new TabSleepService(Store, SleepTabAsync);
        Closed += (_, _) => _sleepService.Stop();
        RegisterKeyboardShortcuts();

        RefreshContentArea();
        ApplyTabLayout();
        SetWindowFrame(Store.Settings.ShowWindowFrame);
    }

    private void SetupWindowAppearance()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var wndId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(wndId);
        ApplyAppIcon(Store.Settings.AppIconVariant);

        if (_appWindow?.TitleBar is { } titleBar)
        {
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(TopBar);
            titleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
            titleBar.BackgroundColor = Colors.Transparent;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(20, 0, 0, 0);
            titleBar.ButtonForegroundColor = Colors.Black;
        }

        _appWindow?.Resize(new Windows.Graphics.SizeInt32(1280, 800));
    }

    public void ApplyAppIcon(string variant)
    {
        var path = UI.AppIconAssets.IconPath(variant);
        if (File.Exists(path)) _appWindow?.SetIcon(path);
        TopBar.TabStrip.SyncAppIcon(variant);
    }

    private void UpdateTitleBarHitRegions()
    {
        if (_appWindow is null || !ExtendsContentIntoTitleBar || TopBar.XamlRoot is null) return;
        var scale = TopBar.XamlRoot.RasterizationScale;
        var elements = new FrameworkElement[]
            { TopBar.TabStrip.InteractiveContent, TopBar.RightControls };
        var passthrough = elements.Where(element => element.ActualWidth > 0 && element.ActualHeight > 0)
            .Select(element =>
            {
                var bounds = element.TransformToVisual(RootGrid).TransformBounds(
                    new Windows.Foundation.Rect(0, 0, element.ActualWidth, element.ActualHeight));
                return new Windows.Graphics.RectInt32(
                    (int)Math.Round(bounds.X * scale),
                    (int)Math.Round(bounds.Y * scale),
                    (int)Math.Round(bounds.Width * scale),
                    (int)Math.Round(bounds.Height * scale));
            }).ToArray();
        InputNonClientPointerSource.GetForWindowId(_appWindow.Id)
            .SetRegionRects(NonClientRegionKind.Passthrough, passthrough);
    }

    private void WireStore()
    {
        Store.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Store.ActiveTab))
                DispatcherQueue.TryEnqueue(() =>
                {
                    HideAddressOverlay();
                    NewTabPageView.HideRecentPages();
                    ShowBrowser();
                    RefreshContentArea();
                });
        };

        foreach (var tab in Store.Tabs) tab.PropertyChanged += OnTabPropertyChanged;
        Store.Tabs.CollectionChanged += OnTabsChanged;

        TopBar.Initialize(Store);
        SidebarView.Initialize(Store);
        TabSwitcherView.Initialize(Store);
        TabSwitcherView.RequestClose += () => TabSwitcherView.Visibility = Visibility.Collapsed;
    }

    private void RefreshContentArea()
    {
        var active = Store.ActiveTab;
        if (active is null) return;
        TopBar.UpdateSiteIdentity();

        bool isNewTab = active.Url == "bow:newtab" || string.IsNullOrEmpty(active.Url);
        var partner = active.SplitPartnerId is Guid partnerId
            ? Store.Tabs.FirstOrDefault(t => t.Id == partnerId) : null;
        bool isSplit = !isNewTab && active.IsSplitPartner && partner is not null;

        NewTabPageView.Visibility = isNewTab ? Visibility.Visible : Visibility.Collapsed;
        foreach (var host in _tabHosts.Values) host.Visibility = Visibility.Collapsed;
        _contentGrid.ColumnDefinitions.Clear();
        _contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _splitDivider.Visibility = isSplit ? Visibility.Visible : Visibility.Collapsed;

        if (!isNewTab)
        {
            var host = GetOrCreateHost(active);
            Grid.SetColumn(host, 0);
            host.Visibility = Visibility.Visible;
            if (isSplit)
            {
                _contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                _contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                Grid.SetColumn(_splitDivider, 1);
                var partnerHost = GetOrCreateHost(partner!);
                Grid.SetColumn(partnerHost, 2);
                partnerHost.Visibility = Visibility.Visible;
            }
        }

        UpdateZenMode(Store.Settings.ZenMode);
    }

    private WebViewHost GetOrCreateHost(BowTab tab)
    {
        if (_tabHosts.TryGetValue(tab.Id, out var host)) return host;
        host = new WebViewHost { Visibility = Visibility.Collapsed };
        host.SetTab(tab);
        _tabHosts.Add(tab.Id, host);
        _contentGrid.Children.Insert(0, host);
        return host;
    }

    public void RecoverTab(Guid id)
    {
        if (!_tabHosts.Remove(id, out var oldHost)) return;
        _contentGrid.Children.Remove(oldHost);
        oldHost.Dispose();
        if (Store.Tabs.FirstOrDefault(tab => tab.Id == id) is { } tab)
        {
            tab.NavigationFailed = false;
            tab.IsLoading = true;
            GetOrCreateHost(tab);
            RefreshContentArea();
        }
    }

    private void OnTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender == Store.ActiveTab && e.PropertyName is nameof(BowTab.HasLoadedSuccessfully)
            or nameof(BowTab.NavigationFailed))
            DispatcherQueue.TryEnqueue(TopBar.UpdateSiteIdentity);
        if (sender == Store.ActiveTab && e.PropertyName is nameof(BowTab.Url) or nameof(BowTab.IsSplitPartner) or nameof(BowTab.SplitPartnerId))
            DispatcherQueue.TryEnqueue(RefreshContentArea);
    }

    private void OnTabsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (BowTab tab in e.OldItems)
            {
                tab.PropertyChanged -= OnTabPropertyChanged;
                if (_tabHosts.Remove(tab.Id, out var host))
                {
                    _contentGrid.Children.Remove(host);
                    host.Dispose();
                }
            }
        if (e.NewItems is not null)
            foreach (BowTab tab in e.NewItems) tab.PropertyChanged += OnTabPropertyChanged;
        DispatcherQueue.TryEnqueue(RefreshContentArea);
    }

    private async Task<bool> SleepTabAsync(BowTab tab)
    {
        if (!_tabHosts.TryGetValue(tab.Id, out var host)) return true;
        if (!await host.CanSleepAsync()) return false;
        await host.SleepAsync();
        if (tab == Store.ActiveTab) return false;
        _contentGrid.Children.Remove(host);
        _tabHosts.Remove(tab.Id);
        host.Dispose();
        return true;
    }

    public void UpdateZenMode(bool zenMode)
    {
        bool entering = zenMode && !_isZenMode;
        _isZenMode = zenMode;
        if (entering)
        {
            HideAddressOverlay();
            ShowBrowser();
        }
        TopBarRow.Height = zenMode ? new GridLength(0) : new GridLength(40);
        TopBar.Visibility = zenMode ? Visibility.Collapsed : Visibility.Visible;
        _zenExitButton.Visibility = zenMode ? Visibility.Visible : Visibility.Collapsed;
        ApplyTabLayout();
        SetWindowFrame(Store.Settings.ShowWindowFrame);
        ApplyZenPresenter();
    }

    public void SetZenMode(bool zenMode)
    {
        if (Store.Settings.ZenMode != zenMode || _isZenMode != zenMode)
        {
            Store.Settings.ZenMode = zenMode;
            SettingsService.Save(Store.Settings);
            UpdateZenMode(zenMode);
        }
        TopBar.SyncZenMode(zenMode);
    }

    private void ApplyZenPresenter()
    {
        if (!_windowActivated || _appWindow is null) return;
        if (_isZenMode)
        {
            if (_appWindow.Presenter?.Kind == AppWindowPresenterKind.FullScreen) return;
            _presenterBeforeZen = _appWindow.Presenter;
            _appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        }
        else if (_appWindow.Presenter?.Kind == AppWindowPresenterKind.FullScreen)
        {
            if (_presenterBeforeZen is not null)
                _appWindow.SetPresenter(_presenterBeforeZen);
            else
                _appWindow.SetPresenter(AppWindowPresenterKind.Default);
            _presenterBeforeZen = null;
        }
    }

    public void SetWindowFrame(bool show)
    {
        show &= !_isZenMode;
        _contentFrame.Margin = show ? new Thickness(8, 0, 8, 8) : new Thickness(0);
        _contentFrame.CornerRadius = show ? new CornerRadius(9) : new CornerRadius(0);
        _contentFrame.BorderThickness = show ? new Thickness(1) : new Thickness(0);
    }

    public void ShowSettings(string? section = null)
    {
        if (_isZenMode) SetZenMode(false);
        HideAddressOverlay();
        if (_settingsView is null)
        {
            _settingsView = new SettingsView(Store);
            _contentLayer.Children.Add(_settingsView);
        }
        else if (section is null) _settingsView.RefreshCurrentSection();
        MainSplitView.Visibility = Visibility.Collapsed;
        _settingsView.Visibility = Visibility.Visible;
        if (section is not null) _settingsView.SelectSection(section);
        TabSwitcherView.Visibility = Visibility.Collapsed;
        _settingsView.Focus(FocusState.Programmatic);
    }

    public void ShowBrowser()
    {
        if (_settingsView is not null) _settingsView.Visibility = Visibility.Collapsed;
        MainSplitView.Visibility = Visibility.Visible;
    }

    public void RefreshVisibleSettings()
    {
        if (_settingsView?.Visibility == Visibility.Visible)
            _settingsView.RefreshCurrentSection();
    }

    public (Uri? Address, Microsoft.Web.WebView2.Core.CoreWebView2? Core,
        bool LoadedSuccessfully, bool NavigationFailed) GetActiveSitePermissionContext()
    {
        if (Store.ActiveTab is not { } active
            || !Uri.TryCreate(active.Url, UriKind.Absolute, out var address)
            || address.Scheme is not ("http" or "https")) return (null, null, false, false);
        _tabHosts.TryGetValue(active.Id, out var host);
        return (address, host?.WebView.CoreWebView2,
            active.HasLoadedSuccessfully, active.NavigationFailed);
    }

    public void ApplyTitleBarTheme()
    {
        if (_appWindow?.TitleBar is not { } titleBar) return;
        titleBar.ButtonForegroundColor = UI.ThemeBrushes.IsDark ? Colors.White : Colors.Black;
        titleBar.ButtonHoverBackgroundColor = UI.ThemeBrushes.IsDark
            ? Windows.UI.Color.FromArgb(255, 64, 64, 69)
            : Windows.UI.Color.FromArgb(255, 224, 224, 228);
    }

    public void ApplyTabLayout()
    {
        bool sidebar = !_isZenMode && Store.Settings.TabLayout == "Sidebar";
        SidebarView.Visibility = sidebar ? Visibility.Visible : Visibility.Collapsed;
        MainSplitView.ColumnDefinitions[0].Width = new GridLength(sidebar ? 220 : 0);
        TopBar.TabStrip.SetLayout(Store.Settings.TabLayout == "Sidebar");
        if (sidebar) SidebarView.Refresh();
    }

    public void FocusOmnibar()
    {
        if (_isZenMode) SetZenMode(false);
        ShowBrowser();
        _addressBox.PlaceholderText = $"Search with {Store.Settings.SearchEngine} or enter address";
        _addressBox.Text = string.Empty;
        RefreshRecentPages();
        _addressOverlay.Visibility = Visibility.Visible;
        _addressBox.Focus(FocusState.Programmatic);
    }

    private void RefreshRecentPages()
    {
        _recentPages.Children.Clear();
        var query = _addressBox.Text.Trim();
        var entries = HistoryService.Instance.Recent(200).Where(entry =>
            query.Length == 0 || entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || entry.Url.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(5);

        foreach (var entry in entries)
        {
            var openTab = Store.Tabs.FirstOrDefault(tab =>
                string.Equals(tab.Url, entry.Url, StringComparison.OrdinalIgnoreCase));
            var row = new Button
            {
                Height = 48,
                Width = _recentPages.ActualWidth > 0 ? _recentPages.ActualWidth - 8 : double.NaN,
                Padding = new Thickness(12, 0, 12, 0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(5)
            };
            var layout = new Grid();
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            FrameworkElement icon = new FontIcon
            {
                FontFamily = new FontFamily("Segoe Fluent Icons"), Glyph = "\uE774",
                FontSize = 15, Foreground = UI.ThemeBrushes.MutedTextBrush,
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center
            };
            if (Uri.TryCreate(entry.FaviconUrl, UriKind.Absolute, out var favicon)
                && favicon.Scheme is "http" or "https")
                icon = new Image
                {
                    Source = new BitmapImage(favicon), Width = 16, Height = 16,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center
                };
            layout.Children.Add(icon);
            var label = new TextBlock
            {
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = UI.ThemeBrushes.TextBrush,
                FontSize = 13
            };
            label.Inlines.Add(new Run { Text = entry.Title });
            label.Inlines.Add(new Run { Text = $"  —  {new Uri(entry.Url).Host}", Foreground = UI.ThemeBrushes.MutedTextBrush });
            Grid.SetColumn(label, 1);
            layout.Children.Add(label);
            if (openTab is not null)
            {
                var switchLabel = new TextBlock
                {
                    Text = "Switch to Tab  ↗", FontSize = 11,
                    Foreground = UI.ThemeBrushes.MutedTextBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(14, 0, 0, 0)
                };
                Grid.SetColumn(switchLabel, 2);
                layout.Children.Add(switchLabel);
            }
            row.Content = layout;
            AutomationProperties.SetName(row, $"{entry.Title}, {new Uri(entry.Url).Host}" +
                (openTab is null ? string.Empty : ", switch to tab"));
            row.Click += (_, _) =>
            {
                HideAddressOverlay();
                if (openTab is not null) Store.SetActiveTab(openTab.Id);
                else if (Store.ActiveTab is { } active) active.Url = entry.Url;
            };
            _recentPages.Children.Add(row);
        }
    }

    private void AddressBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            var input = _addressBox.Text.Trim();
            if (input.Length > 0 && Store.ActiveTab is { } tab)
                tab.Url = SearchService.Resolve(input, Store.Settings);
            HideAddressOverlay();
            e.Handled = true;
        }
    }

    private void HideAddressOverlay()
    {
        if (_addressOverlay.Visibility != Visibility.Visible) return;
        _addressOverlay.Visibility = Visibility.Collapsed;
        if (Store.ActiveTab is { } tab && _tabHosts.TryGetValue(tab.Id, out var host))
            host.WebView.Focus(FocusState.Programmatic);
    }

    public void RegisterKeyboardShortcuts()
    {
        foreach (var accelerator in _shortcutAccelerators)
            RootGrid.KeyboardAccelerators.Remove(accelerator);
        _shortcutAccelerators.Clear();

        foreach (var command in ShortcutCatalog.Commands)
        {
            if (!ShortcutCatalog.TryParse(ShortcutCatalog.GetBinding(Store.Settings, command), out var chord)) continue;
            var keyName = chord.Key switch
            {
                "Plus" => "Add", "Minus" => "Subtract",
                { Length: 1 } digit when char.IsDigit(digit[0]) => "Number" + digit,
                _ => chord.Key
            };
            if (!Enum.TryParse<Windows.System.VirtualKey>(keyName, true, out var key)) continue;
            var modifiers = Windows.System.VirtualKeyModifiers.None;
            if (chord.Control) modifiers |= Windows.System.VirtualKeyModifiers.Control;
            if (chord.Alt) modifiers |= Windows.System.VirtualKeyModifiers.Menu;
            if (chord.Shift) modifiers |= Windows.System.VirtualKeyModifiers.Shift;
            var action = GetShortcutAction(command.Id);
            if (action is not null) AddKeyAccel(key, modifiers, action);
        }
    }

    private Action? GetShortcutAction(string id) => id switch
    {
        "address" => () => TopBar.FocusOmnibar(),
        "new-tab" => () => Store.AddTab("bow:newtab"),
        "close-tab" => () => { if (Store.ActiveTab is { } tab) Store.CloseTab(tab.Id); },
        "reopen-tab" => () => Store.ReopenLastClosedTab(),
        "tab-switcher" => () => TabSwitcherView.Visibility = Visibility.Visible,
        "next-tab" => () => CycleTab(+1),
        "previous-tab" => () => CycleTab(-1),
        "zoom-in" => () => AdjustZoom(+0.1),
        "zoom-out" => () => AdjustZoom(-0.1),
        "zoom-reset" => () => { if (Store.ActiveTab is { } tab) tab.ZoomFactor = 1.0; },
        "settings" => () => TopBar.OpenSettings(),
        "dismiss" => DismissCurrentOverlay,
        _ => null
    };

    private void DismissCurrentOverlay()
    {
        if (_addressOverlay.Visibility == Visibility.Visible)
            HideAddressOverlay();
        else if (_isZenMode)
            SetZenMode(false);
        else if (TabSwitcherView.Visibility == Visibility.Visible)
            TabSwitcherView.Visibility = Visibility.Collapsed;
        else if (_settingsView?.Visibility == Visibility.Visible)
            ShowBrowser();
    }

    private void AddKeyAccel(Windows.System.VirtualKey key, Windows.System.VirtualKeyModifiers mod, System.Action action)
    {
        var accel = new KeyboardAccelerator { Key = key, Modifiers = mod };
        accel.Invoked += (_, e) => { e.Handled = true; action(); };
        RootGrid.KeyboardAccelerators.Add(accel);
        _shortcutAccelerators.Add(accel);
    }

    private void CycleTab(int delta)
    {
        if (Store.Tabs.Count == 0) return;
        var idx = Store.Tabs.IndexOf(Store.ActiveTab!);
        Store.SetActiveTab(Store.Tabs[(idx + delta + Store.Tabs.Count) % Store.Tabs.Count].Id);
    }

    private void AdjustZoom(double delta)
    {
        if (Store.ActiveTab is { } t)
            t.ZoomFactor = System.Math.Round(System.Math.Clamp(t.ZoomFactor + delta, 0.25, 5.0), 2);
    }
}
