using BOW.Core;
using BOW.Services;
using BOW.UI.Downloads;
using BOW.UI.TabStrip;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace BOW.UI.TopBarView;

public sealed class TopBarView : UserControl
{
    private BowStore? _store;
    private readonly Flyout _quickSettingsFlyout;
    private readonly Flyout _siteInfoFlyout;
    private readonly Button _siteInfoButton;
    private readonly FontIcon _siteInfoIcon;

    public TabStripView TabStrip { get; }
    public StackPanel RightControls { get; }
    private readonly QuickSettingsPanel _quickSettings;
    public FontIcon ThemeIcon { get; }

    public TopBarView()
    {
        var root = new Grid
        {
            Height = 40,
            Background = ThemeBrushes.TopBarBrush,
            Padding = new Thickness(10, 0, 12, 0)
        };

        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left side: Tab strip
        TabStrip = new TabStripView();
        Grid.SetColumn(TabStrip, 0);
        root.Children.Add(TabStrip);

        // Right side: Icons
        var rightStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center
        };
        RightControls = rightStack;
        Grid.SetColumn(rightStack, 1);

        _siteInfoButton = CreateIconButton("\uE72E");
        _siteInfoIcon = (FontIcon)_siteInfoButton.Content;
        _siteInfoFlyout = new Flyout();
        _siteInfoFlyout.Opening += (_, _) => _siteInfoFlyout.Content = BuildSiteInfo();
        _siteInfoButton.Flyout = _siteInfoFlyout;
        _siteInfoButton.Visibility = Visibility.Collapsed;
        AutomationProperties.SetName(_siteInfoButton, "Site information");
        rightStack.Children.Add(_siteInfoButton);

        var searchBtn = CreateIconButton("\uE721");
        searchBtn.Click += (_, _) => App.MainWindow?.FocusOmnibar();
        ToolTipService.SetToolTip(searchBtn, "Search or enter URL");
        AutomationProperties.SetName(searchBtn, "Search or enter URL");
        rightStack.Children.Add(searchBtn);

        var downloadsBtn = CreateIconButton("\uE896"); // Downloads
        downloadsBtn.Flyout = new Flyout { Content = new DownloadsPanel() };
        ToolTipService.SetToolTip(downloadsBtn, "Downloads");
        AutomationProperties.SetName(downloadsBtn, "Downloads");
        rightStack.Children.Add(downloadsBtn);

        var themeBtn = CreateIconButton("\uE790"); // Theme
        ThemeIcon = (FontIcon)themeBtn.Content;
        themeBtn.Click += ThemeButton_Click;
        ToolTipService.SetToolTip(themeBtn, "Switch theme");
        AutomationProperties.SetName(themeBtn, "Switch theme");
        rightStack.Children.Add(themeBtn);

        var settingsBtn = CreateIconButton("\uE713"); // Settings
        _quickSettings = new QuickSettingsPanel();
        _quickSettingsFlyout = new Flyout { Content = _quickSettings };
        settingsBtn.Flyout = _quickSettingsFlyout;
        ToolTipService.SetToolTip(settingsBtn, "Quick settings");
        AutomationProperties.SetName(settingsBtn, "Quick settings");
        rightStack.Children.Add(settingsBtn);

        root.Children.Add(rightStack);
        this.Content = root;
    }

    private Button CreateIconButton(string glyph)
    {
        return new Button
        {
            Width = 28,
            Height = 28,
            Padding = new Thickness(0),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(7),
            Foreground = ThemeBrushes.TextBrush,
            Content = new FontIcon
            {
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                Glyph = glyph,
                FontSize = 13
            }
        };
    }

    public void Initialize(BowStore store)
    {
        _store = store;

        TabStrip.Initialize(store);
        _quickSettings.Initialize(store, this);

        UpdateThemeIcon(store.Settings.Theme);
        UpdateSiteIdentity();
    }

    public void FocusOmnibar() => TabStrip.FocusOmnibar();

    public void OpenSettings()
    {
        _quickSettingsFlyout.Hide();
        App.MainWindow?.ShowSettings();
    }

    public void SyncThemeIcon(string theme) => UpdateThemeIcon(theme);

    public void SyncZenMode(bool enabled) => _quickSettings.SyncZenMode(enabled);

    public void UpdateSiteIdentity()
    {
        var tab = _store?.ActiveTab;
        if (!BrowsingSafety.IsWebAddress(tab?.Url))
        {
            _siteInfoButton.Visibility = Visibility.Collapsed;
            return;
        }

        var uri = new Uri(tab!.Url);
        _siteInfoButton.Visibility = Visibility.Visible;
        _siteInfoIcon.Glyph = tab.NavigationFailed || uri.Scheme == "http"
            ? "\uE7BA" : tab.HasLoadedSuccessfully ? "\uE72E" : "\uE774";
        ToolTipService.SetToolTip(_siteInfoButton,
            $"{uri.Host} · {BrowsingSafety.ConnectionDescription(tab.Url,
                tab.HasLoadedSuccessfully, tab.NavigationFailed)}");
    }

    private UIElement BuildSiteInfo()
    {
        var tab = _store?.ActiveTab;
        if (!Uri.TryCreate(tab?.Url, UriKind.Absolute, out var uri))
            return new TextBlock { Text = "No site is open." };

        var panel = new StackPanel { Width = 300, Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = uri.Host,
            FontFamily = ThemeBrushes.UiFont,
            FontSize = 15,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = ThemeBrushes.TextBrush
        });
        panel.Children.Add(new TextBlock
        {
            Text = uri.GetLeftPart(UriPartial.Authority),
            FontFamily = ThemeBrushes.UiFont,
            FontSize = 11,
            Foreground = ThemeBrushes.MutedTextBrush,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new TextBlock
        {
            Text = BrowsingSafety.ConnectionDescription(tab.Url,
                tab.HasLoadedSuccessfully, tab.NavigationFailed),
            FontFamily = ThemeBrushes.UiFont,
            FontSize = 12,
            Foreground = ThemeBrushes.TextBrush,
            TextWrapping = TextWrapping.Wrap
        });
        var permissions = new Button { Content = "Site permissions", Margin = new Thickness(0, 8, 0, 0) };
        permissions.Click += (_, _) =>
        {
            _siteInfoFlyout.Hide();
            App.MainWindow?.ShowSettings("Site permissions");
        };
        panel.Children.Add(permissions);
        return panel;
    }

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_store is null) return;
        var newTheme = ThemeBrushes.IsDark ? "Light" : "Dark";
        App.ChangeTheme(newTheme);
        App.MainWindow?.RefreshVisibleSettings();
    }

    private void UpdateThemeIcon(string theme)
    {
        ThemeIcon.Glyph = theme switch
        {
            "Dark" => "\uE706",
            "Light" => "\uE708",
            _ => "\uE790",
        };
    }
}
