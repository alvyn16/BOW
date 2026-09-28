using BOW.Core;
using BOW.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace BOW.UI.TopBarView;

public sealed class QuickSettingsPanel : UserControl
{
    private BowStore? _store;
    private TopBarView? _topBar;
    private readonly QuietToggle _zenToggle;
    private readonly QuietToggle _frameToggle;
    private readonly QuietToggle _scrollToggle;

    public QuickSettingsPanel()
    {
        var rows = new StackPanel { Spacing = 2 };
        _zenToggle = AddToggle(rows, "\uE72A", "Zen mode", value =>
        {
            App.MainWindow?.SetZenMode(value);
        });
        _frameToggle = AddToggle(rows, "\uE8A9", "Window frame", value =>
        {
            if (_store is null) return;
            _store.Settings.ShowWindowFrame = value;
            App.MainWindow?.SetWindowFrame(value);
            SettingsService.Save(_store.Settings);
        });
        _scrollToggle = AddToggle(rows, "\uE8CB", "Smooth scrolling", value =>
        {
            if (_store is null) return;
            _store.Settings.SmoothScrolling = value;
            SettingsService.Save(_store.Settings);
        });

        rows.Children.Add(new Border
        {
            Height = 1,
            Background = ThemeBrushes.TopBarBorderBrush,
            Margin = new Thickness(4, 8, 4, 6)
        });
        var settingsRow = new Grid();
        settingsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        settingsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        settingsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        settingsRow.Children.Add(new FontIcon
        {
            FontFamily = new FontFamily("Segoe Fluent Icons"), Glyph = "\uE713", FontSize = 13,
            Foreground = ThemeBrushes.TextBrush, VerticalAlignment = VerticalAlignment.Center
        });
        var settingsLabel = new TextBlock
        {
            Text = "All Settings...", FontFamily = ThemeBrushes.UiFont, FontSize = 12,
            Foreground = ThemeBrushes.TextBrush, VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(settingsLabel, 1);
        settingsRow.Children.Add(settingsLabel);
        var chevron = new FontIcon
        {
            FontFamily = new FontFamily("Segoe Fluent Icons"), Glyph = "\uE76C", FontSize = 9,
            Foreground = ThemeBrushes.MutedTextBrush, VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(chevron, 2);
        settingsRow.Children.Add(chevron);
        var settingsButton = new Button
        {
            Content = settingsRow,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Height = 34,
            Padding = new Thickness(8, 0, 8, 0),
            CornerRadius = new CornerRadius(7)
        };
        settingsButton.Click += (_, _) => _topBar?.OpenSettings();
        rows.Children.Add(settingsButton);

        Content = new Border
        {
            Width = 316,
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(12),
            Background = ThemeBrushes.ControlSurfaceBrush,
            BorderBrush = ThemeBrushes.TopBarBorderBrush,
            BorderThickness = new Thickness(1),
            Child = rows
        };
    }

    public void Initialize(BowStore store, TopBarView topBar)
    {
        _store = store;
        _topBar = topBar;
        _zenToggle.IsOn = store.Settings.ZenMode;
        _frameToggle.IsOn = store.Settings.ShowWindowFrame;
        _scrollToggle.IsOn = store.Settings.SmoothScrolling;
    }

    public void SyncZenMode(bool enabled) => _zenToggle.IsOn = enabled;

    private static QuietToggle AddToggle(StackPanel rows, string glyph, string label, Action<bool> changed)
    {
        var row = new Grid { Height = 36, Padding = new Thickness(8, 0, 4, 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(new FontIcon
        {
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            Glyph = glyph,
            FontSize = 13,
            Foreground = ThemeBrushes.TextBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        var text = new TextBlock
        {
            Text = label,
            FontFamily = ThemeBrushes.UiFont,
            FontSize = 12,
            Foreground = ThemeBrushes.TextBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(text, 1);
        row.Children.Add(text);

        var toggle = new QuietToggle(label);
        toggle.Changed += changed;
        Grid.SetColumn(toggle, 2);
        row.Children.Add(toggle);
        rows.Children.Add(row);
        return toggle;
    }
}
