using BOW.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Documents;

namespace BOW.UI.TopBarView;

public sealed class NewTabPage : UserControl
{
    private readonly Border _recentFrame;
    private readonly StackPanel _recentPages;
    private bool _showRecentPages;
    public TextBox SearchBox { get; }

    public NewTabPage()
    {
        var root = new Grid { Background = ThemeBrushes.WindowBackgroundBrush };

        var searchFrame = new Border
        {
            MaxWidth = 480,
            Height = 44,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(24, 0, 24, 0),
            RenderTransform = new TranslateTransform { Y = -165 },
            Background = ThemeBrushes.ControlSurfaceBrush,
            BorderBrush = ThemeBrushes.TopBarBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9)
        };
        var searchGrid = new Grid();
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        searchGrid.Children.Add(new FontIcon
        {
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            Glyph = "\uE721",
            FontSize = 13,
            Foreground = ThemeBrushes.MutedTextBrush,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        SearchBox = new TextBox
        {
            PlaceholderText = "Search or Enter URL...",
            Height = 38,
            FontSize = 13,
            FontFamily = ThemeBrushes.UiFont,
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0, 10, 0, 0),
            Foreground = ThemeBrushes.TextBrush,
            Background = ThemeBrushes.ControlSurfaceBrush,
            BorderThickness = new Thickness(0)
        };
        ThemeBrushes.StyleTextBox(SearchBox);
        AutomationProperties.SetName(SearchBox, "Search or enter URL");
        Grid.SetColumn(SearchBox, 1);
        SearchBox.KeyDown += SearchBox_KeyDown;
        SearchBox.TextChanged += (_, _) => { if (_showRecentPages) RefreshRecentPages(); };
        SearchBox.AddHandler(UIElement.PointerPressedEvent,
            new PointerEventHandler((_, _) => ShowRecentPages()), true);
        searchGrid.Children.Add(SearchBox);
        searchFrame.Child = searchGrid;
        root.Children.Add(searchFrame);

        _recentPages = new StackPanel();
        _recentFrame = new Border
        {
            MaxWidth = 480,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(24, 0, 24, 0),
            Padding = new Thickness(4),
            Background = ThemeBrushes.ControlSurfaceBrush,
            BorderBrush = ThemeBrushes.TopBarBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Visibility = Visibility.Collapsed,
            Child = _recentPages
        };
        root.Children.Add(_recentFrame);
        root.SizeChanged += (_, _) => _recentFrame.Margin = new Thickness(
            24, Math.Max(52, root.ActualHeight / 2 - 165 + 22 + 4), 24, 0);
        root.PointerPressed += (_, e) =>
        {
            var point = e.GetCurrentPoint(root).Position;
            var searchBounds = searchFrame.TransformToVisual(root).TransformBounds(
                new Windows.Foundation.Rect(0, 0, searchFrame.ActualWidth, searchFrame.ActualHeight));
            var recentBounds = _recentFrame.TransformToVisual(root).TransformBounds(
                new Windows.Foundation.Rect(0, 0, _recentFrame.ActualWidth, _recentFrame.ActualHeight));
            if (!searchBounds.Contains(point) && !recentBounds.Contains(point)) HideRecentPages();
        };
        this.Content = root;
        Unloaded += (_, _) => HideRecentPages();
    }

    private void ShowRecentPages()
    {
        _showRecentPages = true;
        RefreshRecentPages();
    }

    public void HideRecentPages()
    {
        _showRecentPages = false;
        _recentFrame.Visibility = Visibility.Collapsed;
    }

    private void RefreshRecentPages()
    {
        _recentPages.Children.Clear();
        var query = SearchBox.Text.Trim();
        var entries = HistoryService.Instance.Recent(200).Where(entry =>
            query.Length == 0 || entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || entry.Url.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(5);

        foreach (var entry in entries)
        {
            var openTab = App.Store.Tabs.FirstOrDefault(tab =>
                string.Equals(tab.Url, entry.Url, StringComparison.OrdinalIgnoreCase));
            var row = new Button
            {
                Height = 44,
                Padding = new Thickness(12, 0, 12, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(5)
            };
            var layout = new Grid();
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            FrameworkElement icon = new FontIcon
            {
                FontFamily = new FontFamily("Segoe Fluent Icons"), Glyph = "\uE774",
                FontSize = 15, Foreground = ThemeBrushes.MutedTextBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (Uri.TryCreate(entry.FaviconUrl, UriKind.Absolute, out var favicon)
                && favicon.Scheme is "http" or "https")
                icon = new Image { Source = new BitmapImage(favicon), Width = 16, Height = 16,
                    VerticalAlignment = VerticalAlignment.Center };
            layout.Children.Add(icon);

            var label = new TextBlock
            {
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = ThemeBrushes.TextBrush,
                FontFamily = ThemeBrushes.UiFont,
                FontSize = 13
            };
            label.Inlines.Add(new Run { Text = entry.Title });
            label.Inlines.Add(new Run { Text = $"  —  {new Uri(entry.Url).Host}",
                Foreground = ThemeBrushes.MutedTextBrush });
            Grid.SetColumn(label, 1);
            layout.Children.Add(label);
            if (openTab is not null)
            {
                var switchLabel = new TextBlock
                {
                    Text = "Switch to Tab  ↗", FontSize = 11,
                    FontFamily = ThemeBrushes.UiFont,
                    Foreground = ThemeBrushes.MutedTextBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 0, 0)
                };
                Grid.SetColumn(switchLabel, 2);
                layout.Children.Add(switchLabel);
            }
            row.Content = layout;
            AutomationProperties.SetName(row, $"{entry.Title}, {new Uri(entry.Url).Host}" +
                (openTab is null ? string.Empty : ", switch to tab"));
            row.Click += (_, _) =>
            {
                if (openTab is not null) App.Store.SetActiveTab(openTab.Id);
                else if (App.Store.ActiveTab is { } active) active.Url = entry.Url;
            };
            _recentPages.Children.Add(row);
        }

        _recentFrame.Visibility = _recentPages.Children.Count == 0
            ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            HideRecentPages();
            e.Handled = true;
            return;
        }
        if (e.Key != Windows.System.VirtualKey.Enter) return;
        var input = SearchBox.Text.Trim();
        if (string.IsNullOrEmpty(input)) return;
        var url = SearchService.Resolve(input, App.Store.Settings);
        HideRecentPages();
        if (App.Store.ActiveTab is not null)
        {
            App.Store.ActiveTab.Url = url;
        }

        e.Handled = true;
    }
}
