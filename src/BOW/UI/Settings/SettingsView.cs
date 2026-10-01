using BOW.Core;
using BOW.Services;
using BOW.UI.Security;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.Web.WebView2.Core;

namespace BOW.UI.Settings;

public sealed class SettingsView : UserControl
{
    private readonly BowStore _store;
    private readonly List<(string Icon, string Label, System.Func<UIElement> Builder)> _sections;
    private readonly List<Button> _navButtons = new();
    private int _selectedSection;

    public Grid ContentArea { get; }

    public SettingsView(BowStore store)
    {
        _store = store;

        var root = new Grid { Background = ThemeBrushes.WindowBackgroundBrush };

        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(188) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var sidebar = new Grid { Background = ThemeBrushes.SidebarBrush };
        var pane = new StackPanel { Spacing = 4, Padding = new Thickness(10, 20, 10, 16) };
        var backButton = new Button
        {
            Content = "←  Back to browser",
            FontFamily = ThemeBrushes.UiFont,
            FontSize = 11,
            Foreground = ThemeBrushes.MutedTextBrush,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Height = 30,
            Padding = new Thickness(10, 0, 10, 0),
            Margin = new Thickness(0, 0, 0, 10)
        };
        backButton.Click += (_, _) => App.MainWindow?.ShowBrowser();
        AutomationProperties.SetAutomationId(backButton, "BackToBrowser");
        pane.Children.Add(backButton);
        pane.Children.Add(new TextBlock
        {
            Text = "Settings",
            FontFamily = ThemeBrushes.UiFont,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 15,
            Foreground = ThemeBrushes.TextBrush,
            Margin = new Thickness(16, 0, 0, 14)
        });
        sidebar.Children.Add(pane);
        sidebar.Children.Add(new Border
        {
            Width = 1,
            Background = ThemeBrushes.TopBarBorderBrush,
            HorizontalAlignment = HorizontalAlignment.Right
        });
        root.Children.Add(sidebar);

        var contentGrid = new Grid { Padding = new Thickness(40, 45, 40, 24) };
        ContentArea = new Grid();
        contentGrid.Children.Add(new ScrollViewer
        {
            Content = ContentArea,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        });
        Grid.SetColumn(contentGrid, 1);
        root.Children.Add(contentGrid);
        this.Content = root;

        _sections = new()
        {
            ("\uE713", "General", BuildGeneralSection),
            ("\uE76E", "Appearance", BuildAppearanceSection),
            ("\uE721", "Search", BuildSearchSection),
            ("\uE943", "Tabs", BuildTabsSection),
            ("\uE72E", "Privacy & browsing", BuildBrowsingSection),
            ("\uE8D7", "Site permissions", BuildSitePermissionsSection),
            ("\uE765", "Keyboard shortcuts", BuildShortcutsSection),
            ("\uE946", "Downloads", BuildDownloadsSection),
            ("\uE823", "About", BuildAboutSection),
        };

        PopulateNav(pane);
        ShowSection(0);
    }

    private void PopulateNav(StackPanel pane)
    {
        for (var i = 0; i < _sections.Count; i++)
        {
            var (icon, label, _) = _sections[i];
            var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            item.Children.Add(new FontIcon
            {
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                Glyph = icon,
                FontSize = 12,
                Foreground = ThemeBrushes.MutedTextBrush
            });
            item.Children.Add(new TextBlock
            {
                Text = label,
                FontFamily = ThemeBrushes.UiFont,
                FontSize = 12,
                Foreground = ThemeBrushes.TextBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            var button = new Button
            {
                Content = item,
                Height = 30,
                Padding = new Thickness(10, 0, 10, 0),
                CornerRadius = new CornerRadius(6),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0)
            };
            var index = i;
            AutomationProperties.SetName(button, label);
            button.Click += (_, _) => ShowSection(index);
            _navButtons.Add(button);
            pane.Children.Add(button);
        }
    }

    private void ShowSection(int index)
    {
        if (index < 0 || index >= _sections.Count) return;
        _selectedSection = index;
        for (var i = 0; i < _navButtons.Count; i++)
            _navButtons[i].Background = i == index ? ThemeBrushes.SelectedBrush : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        ContentArea.Children.Clear();
        ContentArea.Children.Add(_sections[index].Builder());
    }

    public void SelectSection(string label)
    {
        var index = _sections.FindIndex(section => section.Label == label);
        if (index >= 0) ShowSection(index);
    }

    public void RefreshCurrentSection() => ShowSection(_selectedSection);

    private UIElement BuildSitePermissionsSection()
    {
        var panel = MakeSectionPanel("Site permissions");
        var (address, core, loaded, failed) = App.MainWindow?.GetActiveSitePermissionContext()
            ?? (null, null, false, false);
        panel.Children.Add(new SitePermissionsSettingsView(address, core, loaded, failed));
        return panel;
    }

    private UIElement BuildGeneralSection()
    {
        var panel = MakeSectionPanel("General");

        panel.Children.Add(MakeToggleRow(
            "Restore session on start",
            "Reopen your tabs from the previous session.",
            _store.Settings.RestoreSessionOnStart,
            v => { _store.Settings.RestoreSessionOnStart = v; Save(); }));

        panel.Children.Add(MakeSectionLabel("DOWNLOAD FOLDER"));
        var folderRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4, 0, 16) };
        var folderBox = new TextBox
        {
            Text = _store.Settings.DownloadFolder,
            Width = 340,
            IsReadOnly = true,
            FontFamily = ThemeBrushes.UiFont,
            FontSize = 11,
            Foreground = ThemeBrushes.TextBrush,
            Background = ThemeBrushes.ControlSurfaceBrush,
            BorderBrush = ThemeBrushes.TopBarBorderBrush
        };
        ThemeBrushes.StyleTextBox(folderBox);
        var browseBtn = new Button { Content = "Browse…" };
        browseBtn.Click += async (_, _) =>
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker,
                WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow!));
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads;
            var folder = await picker.PickSingleFolderAsync();
            if (folder is not null)
            {
                _store.Settings.DownloadFolder = folder.Path;
                folderBox.Text = folder.Path;
                Save();
            }
        };
        folderRow.Children.Add(folderBox);
        folderRow.Children.Add(browseBtn);
        panel.Children.Add(folderRow);
        panel.Children.Add(MakeToggleRow(
            "Ask where to save each download",
            "Choose a folder when a download starts.",
            _store.Settings.AskWhereToSaveDownloads,
            value => { _store.Settings.AskWhereToSaveDownloads = value; Save(); }));

        return panel;
    }

    private UIElement BuildAppearanceSection()
    {
        var panel = MakeSectionPanel("Appearance");
        var iconButtons = new Dictionary<string, Button>();

        panel.Children.Add(MakeSectionLabel("THEME"));
        panel.Children.Add(MakeSegmentedControl(["Auto", "Light", "Dark"],
            _store.Settings.Theme switch { "Light" => 1, "Dark" => 2, _ => 0 }, index =>
        {
            var t = index switch { 1 => "Light", 2 => "Dark", _ => "Auto" };
            App.ChangeTheme(t);
            foreach (var (name, choice) in iconButtons)
                choice.BorderBrush = name == _store.Settings.AppIconVariant
                    ? ThemeBrushes.AccentBrush : ThemeBrushes.TopBarBorderBrush;
        }));

        panel.Children.Add(MakeSectionLabel("APP ICON"));
        var iconChoices = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Margin = new Thickness(0, 4, 0, 12)
        };
        foreach (var variant in new[] { "White", "Black" })
        {
            var content = new StackPanel
            {
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            content.Children.Add(new Image
            {
                Source = AppIconAssets.Preview(variant),
                Width = 52, Height = 52,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            content.Children.Add(new TextBlock
            {
                Text = variant,
                FontFamily = ThemeBrushes.UiFont,
                FontSize = 12,
                Foreground = ThemeBrushes.TextBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            var button = new Button
            {
                Content = content,
                Width = 112, Height = 96,
                CornerRadius = new CornerRadius(8),
                Background = ThemeBrushes.ControlSurfaceBrush,
                BorderThickness = new Thickness(2),
                BorderBrush = ThemeBrushes.TopBarBorderBrush
            };
            AutomationProperties.SetName(button, $"{variant} app icon");
            button.Click += (_, _) =>
            {
                _store.Settings.AppIconVariant = variant;
                Save();
                App.MainWindow?.ApplyAppIcon(variant);
                foreach (var (name, choice) in iconButtons)
                    choice.BorderBrush = name == variant
                        ? ThemeBrushes.AccentBrush : ThemeBrushes.TopBarBorderBrush;
            };
            iconButtons.Add(variant, button);
            iconChoices.Children.Add(button);
        }
        iconButtons[AppIconAssets.Normalize(_store.Settings.AppIconVariant)].BorderBrush =
            ThemeBrushes.AccentBrush;
        panel.Children.Add(iconChoices);

        panel.Children.Add(MakeSectionLabel("TAB LAYOUT"));
        panel.Children.Add(MakeSegmentedControl(["Strip", "Sidebar"],
            _store.Settings.TabLayout == "Sidebar" ? 1 : 0, index =>
        {
            _store.Settings.TabLayout = index == 1 ? "Sidebar" : "Strip";
            Save();
            App.MainWindow?.ApplyTabLayout();
        }));

        return panel;
    }

    private UIElement BuildSearchSection()
    {
        var panel = MakeSectionPanel("Search");

        panel.Children.Add(MakeSectionLabel("DEFAULT SEARCH ENGINE"));
        string[] engines = ["DuckDuckGo", "Startpage", "Google"];
        var selected = Array.IndexOf(engines, _store.Settings.SearchEngine);
        if (selected < 0)
        {
            selected = 0;
            _store.Settings.SearchEngine = engines[0];
            Save();
        }
        var engineControl = MakeSegmentedControl(engines, selected, index =>
        {
            _store.Settings.SearchEngine = engines[index];
            Save();
        });
        engineControl.Width = 340;
        panel.Children.Add(engineControl);

        return panel;
    }

    private UIElement BuildTabsSection()
    {
        var panel = MakeSectionPanel("Tabs");

        panel.Children.Add(MakeSectionLabel("TAB SLEEP TIMER"));
        var sleepSlider = new Slider
        {
            Minimum = 0, Maximum = 60,
            Value = _store.Settings.TabSleepMinutes,
            StepFrequency = 1, Width = 260,
            Margin = new Thickness(0, 4, 0, 4)
        };
        var sleepLabel = new TextBlock
        {
            Text = SleepIntervalLabel(_store.Settings.TabSleepMinutes),
            FontSize = 12,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 136, 136, 136))
        };
        sleepSlider.ValueChanged += (_, e) =>
        {
            var val = (int)e.NewValue;
            _store.Settings.TabSleepMinutes = val;
            sleepLabel.Text = SleepIntervalLabel(val);
            Save();
        };
        panel.Children.Add(sleepSlider);
        panel.Children.Add(sleepLabel);

        panel.Children.Add(new TextBlock
        {
            Text = "Inactive tabs can be unloaded or suspended to free memory. Playing media and active tabs stay awake.",
            FontSize = 10, Foreground = ThemeBrushes.MutedTextBrush,
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8)
        });
        panel.Children.Add(MakeSectionLabel("NEVER SLEEP THESE SITES"));
        var exceptions = new StackPanel { Spacing = 4 };
        void RefreshExceptions()
        {
            exceptions.Children.Clear();
            foreach (var host in (_store.Settings.TabSleepExcludedHosts ?? []).OrderBy(value => value))
            {
                var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.Children.Add(new TextBlock
                {
                    Text = host, FontSize = 12, Foreground = ThemeBrushes.TextBrush,
                    VerticalAlignment = VerticalAlignment.Center
                });
                var remove = new Button { Content = "Remove", FontSize = 11 };
                remove.Click += (_, _) =>
                {
                    _store.Settings.TabSleepExcludedHosts?.Remove(host);
                    Save();
                    RefreshExceptions();
                };
                Grid.SetColumn(remove, 1);
                row.Children.Add(remove);
                exceptions.Children.Add(row);
            }
            if (exceptions.Children.Count == 0)
                exceptions.Children.Add(new TextBlock
                {
                    Text = "No site exceptions", FontSize = 11,
                    Foreground = ThemeBrushes.MutedTextBrush
                });
        }
        RefreshExceptions();
        panel.Children.Add(exceptions);
        var addRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8,
            Margin = new Thickness(0, 9, 0, 0) };
        var hostInput = new TextBox { PlaceholderText = "example.com", Width = 220, FontSize = 11 };
        var add = new Button { Content = "Add site", FontSize = 11 };
        var exceptionStatus = new TextBlock { FontSize = 10, Foreground = ThemeBrushes.MutedTextBrush };
        add.Click += (_, _) =>
        {
            var input = hostInput.Text.Trim();
            var candidate = input.Contains("://", StringComparison.Ordinal)
                ? input : "https://" + input;
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https")
                || string.IsNullOrWhiteSpace(uri.Host))
            {
                exceptionStatus.Text = "Enter a valid website host.";
                return;
            }
            _store.Settings.TabSleepExcludedHosts ??= [];
            if (!_store.Settings.TabSleepExcludedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
                _store.Settings.TabSleepExcludedHosts.Add(uri.Host);
            Save();
            hostInput.Text = "";
            exceptionStatus.Text = "";
            RefreshExceptions();
        };
        addRow.Children.Add(hostInput);
        addRow.Children.Add(add);
        panel.Children.Add(addRow);
        panel.Children.Add(exceptionStatus);

        return panel;
    }

    private static string SleepIntervalLabel(int minutes) => minutes == 0
        ? "Off" : minutes == 1 ? "1 minute" : $"{minutes} minutes";

    private UIElement BuildBrowsingSection()
    {
        var panel = MakeSectionPanel("Privacy & browsing");
        panel.Children.Add(MakeSectionLabel("RENDERING ENGINE"));
        var engine = new StackPanel { Spacing = 3 };
        engine.Children.Add(new TextBlock
        {
            Text = "Microsoft Edge WebView2",
            FontFamily = ThemeBrushes.UiFont,
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = ThemeBrushes.TextBrush
        });
        engine.Children.Add(new TextBlock
        {
            Text = "Uses the WebView2 runtime installed on Windows.",
            FontFamily = ThemeBrushes.UiFont,
            FontSize = 10,
            Foreground = ThemeBrushes.MutedTextBrush,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new Border
        {
            Child = engine,
            Padding = new Thickness(12),
            Margin = new Thickness(0, 3, 0, 15),
            Background = ThemeBrushes.ControlSurfaceBrush,
            BorderBrush = ThemeBrushes.TopBarBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8)
        });
        panel.Children.Add(MakeSectionLabel("SCROLLING MECHANICS"));
        panel.Children.Add(MakeToggleRow(
            "Smooth scrolling",
            "Fluid momentum physics for page navigation.",
            _store.Settings.SmoothScrolling,
            v => { _store.Settings.SmoothScrolling = v; Save(); }));
        panel.Children.Add(MakeSectionLabel("TRACKING PROTECTION"));
        var trackingLevels = new[] { "Off", "Basic", "Balanced", "Strict" };
        var selectedLevel = Array.IndexOf(trackingLevels, _store.Settings.TrackingProtectionLevel);
        var trackingStatus = new TextBlock
        {
            FontSize = 10, Foreground = ThemeBrushes.MutedTextBrush,
            TextWrapping = TextWrapping.Wrap
        };
        panel.Children.Add(MakeSegmentedControl(trackingLevels,
            selectedLevel < 0 ? 2 : selectedLevel, index =>
            {
                _store.Settings.TrackingProtectionLevel = trackingLevels[index];
                Save();
                try
                {
                    App.MainWindow?.ApplyTrackingProtection();
                    trackingStatus.Text = "";
                }
                catch (Exception ex)
                {
                    trackingStatus.Text = $"Could not apply this level to open tabs: {ex.Message}";
                }
            }));
        panel.Children.Add(new TextBlock
        {
            Text = "Basic blocks fewer trackers, Balanced is the default, and Strict blocks more but may affect some sites. Off disables WebView2 tracking prevention.",
            FontSize = 10, Foreground = ThemeBrushes.MutedTextBrush,
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8)
        });
        panel.Children.Add(trackingStatus);
        panel.Children.Add(MakeSectionLabel("CLEAR BROWSING DATA"));
        var timeRange = new ComboBox { Width = 220,
            Margin = new Thickness(0, 4, 0, 10) };
        foreach (var label in new[] { "Last hour", "Last 24 hours", "Last 7 days", "Last 4 weeks", "All time" })
            timeRange.Items.Add(label);
        timeRange.SelectedIndex = 4;
        panel.Children.Add(timeRange);
        var history = new CheckBox { Content = "Browsing history", IsChecked = true };
        var siteData = new CheckBox { Content = "Cookies and site data", IsChecked = true };
        var cache = new CheckBox { Content = "Cached images and files", IsChecked = true };
        var downloads = new CheckBox { Content = "Download history", IsChecked = false };
        foreach (var option in new[] { history, siteData, cache, downloads })
            panel.Children.Add(option);
        panel.Children.Add(new TextBlock
        {
            Text = "Clearing site data may sign you out. Download history removes records, not files on disk. Saved passwords and autofill are not cleared.",
            FontSize = 10, Foreground = ThemeBrushes.MutedTextBrush,
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 8)
        });
        var clearData = new Button { Content = "Clear selected data", Margin = new Thickness(0, 8, 0, 0) };
        var clearStatus = new TextBlock
        {
            FontFamily = ThemeBrushes.UiFont,
            FontSize = 11,
            Foreground = ThemeBrushes.MutedTextBrush,
            Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap
        };
        clearData.Click += async (_, _) =>
        {
            var kinds = (CoreWebView2BrowsingDataKinds)0;
            if (history.IsChecked == true) kinds |= CoreWebView2BrowsingDataKinds.BrowsingHistory;
            if (siteData.IsChecked == true) kinds |= CoreWebView2BrowsingDataKinds.AllSite
                | CoreWebView2BrowsingDataKinds.ServiceWorkers;
            if (cache.IsChecked == true) kinds |= CoreWebView2BrowsingDataKinds.DiskCache;
            if (downloads.IsChecked == true) kinds |= CoreWebView2BrowsingDataKinds.DownloadHistory;
            if (kinds == 0)
            {
                clearStatus.Text = "Choose at least one type of data.";
                return;
            }
            if (XamlRoot is null || App.MainWindow is not { } window) return;
            var confirmation = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Clear selected browsing data?",
                Content = "This removes the selected data from BOW and its browser profile for the chosen time range. Open pages may need a reload to reflect the change.",
                PrimaryButtonText = "Clear data",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close
            };
            if (await confirmation.ShowAsync() != ContentDialogResult.Primary) return;
            var since = timeRange.SelectedIndex switch
            {
                0 => DateTimeOffset.UtcNow.AddHours(-1),
                1 => DateTimeOffset.UtcNow.AddDays(-1),
                2 => DateTimeOffset.UtcNow.AddDays(-7),
                3 => DateTimeOffset.UtcNow.AddDays(-28),
                _ => (DateTimeOffset?)null
            };
            clearData.IsEnabled = false;
            clearStatus.Text = "Clearing data…";
            try
            {
                await window.ClearWebViewDataAsync(kinds, since?.UtcDateTime);
                if (history.IsChecked == true)
                {
                    HistoryService.Instance.ClearSince(since);
                    _store.ClearRecentlyClosedSince(since);
                }
                if (downloads.IsChecked == true) DownloadService.Instance.ClearHistorySince(since);
                clearStatus.Text = "Selected browsing data cleared.";
            }
            catch (Exception ex)
            {
                clearStatus.Text = $"Could not finish clearing data: {ex.Message}";
            }
            finally { clearData.IsEnabled = true; }
        };
        panel.Children.Add(clearData);
        panel.Children.Add(clearStatus);
        return panel;
    }

    private UIElement BuildShortcutsSection()
    {
        var panel = MakeSectionPanel("Keyboard shortcuts");
        panel.HorizontalAlignment = HorizontalAlignment.Stretch;
        var reset = new Button
        {
            Content = "Restore default shortcuts",
            FontSize = 12,
            Foreground = ThemeBrushes.TextBrush,
            Background = ThemeBrushes.ControlSurfaceBrush,
            BorderBrush = ThemeBrushes.TopBarBorderBrush,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 12)
        };
        reset.Click += (_, _) =>
        {
            _store.Settings.KeyboardShortcuts = new Dictionary<string, string>();
            Save();
            App.MainWindow?.RegisterKeyboardShortcuts();
            ShowSection(_sections.FindIndex(section => section.Label == "Keyboard shortcuts"));
        };
        panel.Children.Add(reset);

        foreach (var command in ShortcutCatalog.Commands)
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock
            {
                Text = command.Label,
                FontSize = 12,
                Foreground = ThemeBrushes.TextBrush,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 12, 0)
            });
            var bindingButton = new Button
            {
                Content = ShortcutCatalog.GetBinding(_store.Settings, command),
                MinWidth = 116,
                Height = 30,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                FontSize = 11,
                Foreground = ThemeBrushes.TextBrush,
                Background = ThemeBrushes.SidebarBrush,
                BorderBrush = ThemeBrushes.TopBarBorderBrush,
                BorderThickness = new Thickness(1)
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(bindingButton,
                $"Change shortcut for {command.Label}, currently {bindingButton.Content}");
            var editor = CreateShortcutEditor(command, bindingButton);
            bindingButton.Click += (_, _) => editor.Visibility = editor.Visibility == Visibility.Visible
                ? Visibility.Collapsed : Visibility.Visible;
            Grid.SetColumn(bindingButton, 1);
            row.Children.Add(bindingButton);
            var card = new StackPanel();
            card.Children.Add(row);
            card.Children.Add(editor);
            panel.Children.Add(new Border
            {
                Child = card,
                Padding = new Thickness(12, 8, 12, 8),
                Background = ThemeBrushes.ControlSurfaceBrush,
                BorderBrush = ThemeBrushes.TopBarBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 6)
            });
        }
        return panel;
    }

    private Border CreateShortcutEditor(ShortcutDefinition command, Button bindingButton)
    {
        ShortcutCatalog.TryParse(ShortcutCatalog.GetBinding(_store.Settings, command), out var current);
        var modifiers = new ComboBox
        {
            Header = "Modifiers",
            ItemsSource = new[] { "None", "Ctrl", "Ctrl+Shift", "Ctrl+Alt", "Alt", "Alt+Shift" },
            SelectedItem = current.Control
                ? current.Alt ? "Ctrl+Alt" : current.Shift ? "Ctrl+Shift" : "Ctrl"
                : current.Alt ? current.Shift ? "Alt+Shift" : "Alt" : "None",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var keys = Enumerable.Range('A', 26).Select(code => ((char)code).ToString())
            .Concat(Enumerable.Range(0, 10).Select(number => number.ToString()))
            .Concat(["Tab", "Left", "Right", "Plus", "Minus", "Escape"])
            .Concat(Enumerable.Range(1, 12).Select(number => $"F{number}"))
            .ToArray();
        var key = new ComboBox
        {
            Header = "Key",
            ItemsSource = keys,
            SelectedItem = current.Key,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var error = new TextBlock
        {
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 200, 65, 65)),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12
        };
        var editor = new StackPanel { Spacing = 10 };
        editor.Children.Add(new TextBlock
        {
            Text = "Choose the keys for this command. Shortcuts cannot be shared by two commands.",
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ThemeBrushes.MutedTextBrush
        });
        editor.Children.Add(modifiers);
        editor.Children.Add(key);
        editor.Children.Add(error);
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var saveButton = new Button
        {
            Content = "Save shortcut",
            MinWidth = 104,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
            Background = ThemeBrushes.AccentBrush,
            BorderThickness = new Thickness(0)
        };
        var cancelButton = new Button
        {
            Content = "Cancel",
            MinWidth = 76,
            Foreground = ThemeBrushes.TextBrush,
            Background = ThemeBrushes.ControlSurfaceBrush,
            BorderBrush = ThemeBrushes.TopBarBorderBrush,
            BorderThickness = new Thickness(1)
        };
        actions.Children.Add(saveButton);
        actions.Children.Add(cancelButton);
        editor.Children.Add(actions);
        var editorFrame = new Border
        {
            Child = editor,
            Padding = new Thickness(0, 12, 0, 4),
            Margin = new Thickness(0, 8, 0, 0),
            BorderBrush = ThemeBrushes.TopBarBorderBrush,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Visibility = Visibility.Collapsed
        };
        saveButton.Click += (_, _) =>
        {
            var prefix = modifiers.SelectedItem?.ToString();
            var binding = (prefix == "None" ? "" : prefix + "+") + key.SelectedItem;
            if (!ShortcutCatalog.TrySetBinding(_store.Settings, command.Id, binding, out var message))
            {
                error.Text = message;
                return;
            }
            error.Text = string.Empty;
            Save();
            App.MainWindow?.RegisterKeyboardShortcuts();
            bindingButton.Content = ShortcutCatalog.GetBinding(_store.Settings, command);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(bindingButton,
                $"Change shortcut for {command.Label}, currently {bindingButton.Content}");
            editorFrame.Visibility = Visibility.Collapsed;
        };
        cancelButton.Click += (_, _) =>
        {
            error.Text = string.Empty;
            editorFrame.Visibility = Visibility.Collapsed;
        };
        return editorFrame;
    }

    private UIElement BuildDownloadsSection()
    {
        var panel = MakeSectionPanel("Downloads");
        panel.Children.Add(new TextBlock
        {
            Text = "Download folder is configured in General.",
            FontSize = 13,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 136, 136, 136))
        });
        return panel;
    }

    private UIElement BuildAboutSection()
    {
        var panel = MakeSectionPanel("About BOW");
        panel.Children.Add(new UpdateSettingsPanel(_store.Settings));
        panel.Children.Add(new HyperlinkButton
        {
            Content = "Inspired by Lean Browser",
            NavigateUri = new System.Uri("https://github.com/DeepanshuMishraa/lean"),
            Padding = new Thickness(0)
        });
        return panel;
    }

    private static StackPanel MakeSectionPanel(string title)
    {
        var panel = new StackPanel { Spacing = 0, MaxWidth = 520, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontFamily = ThemeBrushes.UiFont,
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = ThemeBrushes.TextBrush,
            Margin = new Thickness(0, 0, 0, 3)
        });
        var description = title switch
        {
            "Privacy & browsing" => "Control tracking and stored browsing data",
            "Appearance" => "Choose how BOW looks and feels",
            "General" => "Startup and file preferences",
            "Search" => "Search from the address bar",
            "Tabs" => "Manage open pages and memory",
            "Downloads" => "Saved files and download behavior",
            "Keyboard shortcuts" => "View and change browser key bindings",
            "Site permissions" => "Control access for the active website",
            _ => "A quieter way to browse"
        };
        panel.Children.Add(new TextBlock
        {
            Text = description,
            FontFamily = ThemeBrushes.UiFont,
            FontSize = 11,
            Foreground = ThemeBrushes.MutedTextBrush,
            Margin = new Thickness(0, 0, 0, 24)
        });
        return panel;
    }

    private static Border MakeSegmentedControl(string[] labels, int selected, Action<int> changed)
    {
        var grid = new Grid();
        var buttons = new List<Button>();
        for (var i = 0; i < labels.Length; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var index = i;
            var button = new Button
            {
                Content = labels[i],
                Height = 29,
                FontFamily = ThemeBrushes.UiFont,
                FontSize = 11,
                Foreground = ThemeBrushes.TextBrush,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(6),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Background = i == selected ? ThemeBrushes.TabActiveBrush : new SolidColorBrush(Microsoft.UI.Colors.Transparent)
            };
            button.Click += (_, _) =>
            {
                for (var j = 0; j < buttons.Count; j++)
                    buttons[j].Background = j == index ? ThemeBrushes.TabActiveBrush : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                changed(index);
            };
            buttons.Add(button);
            Grid.SetColumn(button, i);
            grid.Children.Add(button);
        }
        return new Border
        {
            Child = grid,
            Background = ThemeBrushes.SidebarBrush,
            BorderBrush = ThemeBrushes.TopBarBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(2),
            Height = 34,
            Margin = new Thickness(0, 2, 0, 16)
        };
    }

    private static UIElement MakeSectionLabel(string text) => new TextBlock
    {
        Text = text,
        FontSize = 10,
        FontFamily = ThemeBrushes.UiFont,
        Foreground = ThemeBrushes.MutedTextBrush,
        CharacterSpacing = 60,
        Margin = new Thickness(0, 12, 0, 5)
    };

    private static UIElement MakeToggleRow(string label, string description, bool initialValue, System.Action<bool> onChange)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var textStack = new StackPanel { Spacing = 2 };
        textStack.Children.Add(new TextBlock
        {
            Text = label,
            FontFamily = ThemeBrushes.UiFont,
            FontSize = 12,
            Foreground = ThemeBrushes.TextBrush,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        textStack.Children.Add(new TextBlock
        {
            Text = description,
            FontFamily = ThemeBrushes.UiFont,
            FontSize = 10,
            Foreground = ThemeBrushes.MutedTextBrush,
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetColumn(textStack, 0);

        var toggle = new QuietToggle(label) { IsOn = initialValue, VerticalAlignment = VerticalAlignment.Center };
        toggle.Changed += onChange;
        Grid.SetColumn(toggle, 1);

        grid.Children.Add(textStack);
        grid.Children.Add(toggle);

        return new Border
        {
            Child = grid,
            Padding = new Thickness(12),
            Background = ThemeBrushes.ControlSurfaceBrush,
            BorderBrush = ThemeBrushes.TopBarBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 3, 0, 8)
        };
    }

    private void Save() => SettingsService.Save(_store.Settings);

}
