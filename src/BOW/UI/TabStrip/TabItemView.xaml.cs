using BOW.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace BOW.UI.TabStrip;

public sealed class TabItemView : UserControl
{
    private BowTab? _tab;
    private BowStore? _store;

    public Grid RootGrid { get; }
    public Image FaviconImage { get; }
    public Ellipse FallbackDot { get; }
    public FontIcon PinIcon { get; }
    public FontIcon MuteIcon { get; }
    public Border ActiveBar { get; }

    public TabItemView()
    {
        RootGrid = new Grid
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(7),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0))
        };

        var innerGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        FaviconImage = new Image { Width = 16, Height = 16, Visibility = Visibility.Collapsed };
        innerGrid.Children.Add(FaviconImage);

        FallbackDot = new Ellipse { Width = 12, Height = 12, Fill = new SolidColorBrush(Microsoft.UI.Colors.Gray), Visibility = Visibility.Visible };
        innerGrid.Children.Add(FallbackDot);


        RootGrid.Children.Add(innerGrid);

        PinIcon = new FontIcon
        {
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            Glyph = "\uE840",
            FontSize = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(2),
            Visibility = Visibility.Collapsed
        };
        RootGrid.Children.Add(PinIcon);

        MuteIcon = new FontIcon
        {
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            Glyph = "\uE74F",
            FontSize = 9,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(2),
            Visibility = Visibility.Collapsed
        };
        RootGrid.Children.Add(MuteIcon);

        ActiveBar = new Border
        {
            Height = 2,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Bottom,
            Background = ThemeBrushes.AccentBrush,
            CornerRadius = new CornerRadius(1),
            Visibility = Visibility.Collapsed
        };
        RootGrid.Children.Add(ActiveBar);

        this.Content = RootGrid;

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (_tab is not null)
            _tab.PropertyChanged -= OnTabPropertyChanged;
        if (_store is not null)
            _store.PropertyChanged -= OnStorePropertyChanged;

        _tab = null;
        _store = null;

        if (args.NewValue is TabItemViewModel vm)
        {
            _tab = vm.Tab;
            _store = App.Store;
            _tab.PropertyChanged += OnTabPropertyChanged;
            _store.PropertyChanged += OnStorePropertyChanged;
            ToolTipService.SetToolTip(this, _tab.Title);
            Refresh();
        }
    }

    private void OnTabPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => DispatcherQueue.TryEnqueue(Refresh);

    private void OnStorePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BowStore.ActiveTab))
            DispatcherQueue.TryEnqueue(RefreshActiveState);
    }

    private void Refresh()
    {
        if (_tab is null) return;

        if (Uri.TryCreate(_tab.FaviconUrl, UriKind.Absolute, out var faviconUri))
        {
            FaviconImage.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(faviconUri);
            FaviconImage.Visibility = Visibility.Visible;
            FallbackDot.Visibility = Visibility.Collapsed;
        }
        else
        {
            FaviconImage.Visibility = Visibility.Collapsed;
            FallbackDot.Visibility = _tab.IsLoading ? Visibility.Collapsed : Visibility.Visible;
        }


        PinIcon.Visibility = _tab.IsPinned ? Visibility.Visible : Visibility.Collapsed;
        MuteIcon.Visibility = _tab.IsMuted ? Visibility.Visible : Visibility.Collapsed;
        ToolTipService.SetToolTip(this, _tab.IsMuted ? $"{_tab.Title} · Muted" : _tab.Title);

        RefreshActiveState();
    }

    private void RefreshActiveState()
    {
        if (_tab is null || _store is null) return;
        bool isActive = _store.ActiveTab?.Id == _tab.Id;

        RootGrid.Background = isActive
            ? ThemeBrushes.TabActiveBrush
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        ActiveBar.Visibility = Visibility.Collapsed;
    }
}
