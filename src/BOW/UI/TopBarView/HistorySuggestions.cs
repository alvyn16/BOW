using BOW.Core;
using BOW.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace BOW.UI.TopBarView;

internal sealed class HistorySuggestions
{
    private readonly SuggestionRow[] _rows;

    public int VisibleCount { get; private set; }

    public HistorySuggestions(StackPanel panel, double rowHeight, double iconWidth,
        double switchMargin, Action<HistoryEntry, BowTab?> onSelected)
    {
        _rows = Enumerable.Range(0, 5)
            .Select(_ => new SuggestionRow(rowHeight, iconWidth, switchMargin, onSelected))
            .ToArray();
        foreach (var row in _rows) panel.Children.Add(row.Button);
    }

    public void Refresh(string query, BowStore store)
    {
        var matches = HistoryService.Instance.Search(query, _rows.Length);
        var openTabs = new Dictionary<string, BowTab>(StringComparer.OrdinalIgnoreCase);
        foreach (var tab in store.Tabs)
            if (!string.IsNullOrEmpty(tab.Url)) openTabs.TryAdd(tab.Url, tab);

        VisibleCount = matches.Count;
        for (var i = 0; i < _rows.Length; i++)
        {
            if (i >= matches.Count)
            {
                _rows[i].Button.Visibility = Visibility.Collapsed;
                continue;
            }
            var entry = matches[i];
            openTabs.TryGetValue(entry.Url, out var openTab);
            _rows[i].Update(entry, openTab);
        }
    }

    private sealed class SuggestionRow
    {
        private readonly Action<HistoryEntry, BowTab?> _onSelected;
        private readonly FontIcon _fallbackIcon;
        private readonly Image _faviconIcon;
        private readonly Run _title;
        private readonly Run _host;
        private readonly TextBlock _switchLabel;
        private HistoryEntry? _entry;
        private BowTab? _openTab;

        public Button Button { get; }

        public SuggestionRow(double height, double iconWidth, double switchMargin,
            Action<HistoryEntry, BowTab?> onSelected)
        {
            _onSelected = onSelected;
            Button = new Button
            {
                Height = height,
                Padding = new Thickness(12, 0, 12, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(5),
                Visibility = Visibility.Collapsed
            };
            var layout = new Grid();
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(iconWidth) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icons = new Grid();
            _fallbackIcon = new FontIcon
            {
                FontFamily = new FontFamily("Segoe Fluent Icons"), Glyph = "\uE774",
                FontSize = 15, Foreground = ThemeBrushes.MutedTextBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            _faviconIcon = new Image
            {
                Width = 16, Height = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            icons.Children.Add(_fallbackIcon);
            icons.Children.Add(_faviconIcon);
            layout.Children.Add(icons);

            var label = new TextBlock
            {
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = ThemeBrushes.TextBrush,
                FontFamily = ThemeBrushes.UiFont,
                FontSize = 13
            };
            _title = new Run();
            _host = new Run { Foreground = ThemeBrushes.MutedTextBrush };
            label.Inlines.Add(_title);
            label.Inlines.Add(_host);
            Grid.SetColumn(label, 1);
            layout.Children.Add(label);

            _switchLabel = new TextBlock
            {
                Text = "Switch to Tab  ↗", FontSize = 11,
                FontFamily = ThemeBrushes.UiFont,
                Foreground = ThemeBrushes.MutedTextBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(switchMargin, 0, 0, 0),
                Visibility = Visibility.Collapsed
            };
            Grid.SetColumn(_switchLabel, 2);
            layout.Children.Add(_switchLabel);
            Button.Content = layout;
            Button.Click += (_, _) =>
            {
                if (_entry is not null) _onSelected(_entry, _openTab);
            };
        }

        public void Update(HistoryEntry entry, BowTab? openTab)
        {
            Button.Visibility = Visibility.Visible;
            if (_entry == entry && _openTab == openTab) return;
            _entry = entry;
            _openTab = openTab;
            var host = Uri.TryCreate(entry.Url, UriKind.Absolute, out var uri) ? uri.Host : entry.Url;
            _title.Text = entry.Title;
            _host.Text = $"  —  {host}";
            if (Uri.TryCreate(entry.FaviconUrl, UriKind.Absolute, out var favicon)
                && favicon.Scheme is "http" or "https")
            {
                _faviconIcon.Source = new BitmapImage(favicon);
                _faviconIcon.Visibility = Visibility.Visible;
                _fallbackIcon.Visibility = Visibility.Collapsed;
            }
            else
            {
                _faviconIcon.Source = null;
                _faviconIcon.Visibility = Visibility.Collapsed;
                _fallbackIcon.Visibility = Visibility.Visible;
            }
            _switchLabel.Visibility = openTab is null ? Visibility.Collapsed : Visibility.Visible;
            AutomationProperties.SetName(Button, $"{entry.Title}, {host}" +
                (openTab is null ? string.Empty : ", switch to tab"));
        }
    }
}
