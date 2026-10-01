using BOW.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace BOW.UI.TopBarView;

public sealed class NewTabPage : UserControl
{
    private readonly Border _recentFrame;
    private readonly StackPanel _recentPages;
    private readonly HistorySuggestions _historySuggestions;
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
        _historySuggestions = new HistorySuggestions(_recentPages, 44, 30, 10, (entry, openTab) =>
        {
            if (openTab is not null && App.Store.Tabs.Contains(openTab)) App.Store.SetActiveTab(openTab.Id);
            else if (App.Store.ActiveTab is { } active) active.Url = entry.Url;
        });
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
        _historySuggestions.Refresh(SearchBox.Text, App.Store);
        _recentFrame.Visibility = _historySuggestions.VisibleCount == 0
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
