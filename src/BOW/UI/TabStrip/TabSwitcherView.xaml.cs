using BOW.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace BOW.UI.TabStrip;

public sealed class TabSwitcherView : UserControl
{
    private BowStore? _store;
    public event System.Action? RequestClose;
    public StackPanel TabListView { get; private set; }

    public TabSwitcherView()
    {
        var root = new Grid { Background = new SolidColorBrush(Windows.UI.Color.FromArgb(180, 0, 0, 0)) };
        root.PointerPressed += (_, _) => RequestClose?.Invoke();

        var container = new Grid
        {
            Width = 600,
            Height = 400,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = ThemeBrushes.WindowBackgroundBrush,
            CornerRadius = new CornerRadius(8)
        };
        // Prevent closing when clicking inside the list container
        container.PointerPressed += (s, e) => e.Handled = true;

        var scrollView = new ScrollViewer();
        TabListView = new StackPanel();
        scrollView.Content = TabListView;

        container.Children.Add(scrollView);
        root.Children.Add(container);

        this.Content = root;
    }

    private object CreateTemplate()
    {
        return new TabSwitcherElementFactory();
    }

    private class TabSwitcherElementFactory : Microsoft.UI.Xaml.IElementFactory
    {
        public Microsoft.UI.Xaml.UIElement GetElement(Microsoft.UI.Xaml.ElementFactoryGetArgs args)
        {
            var grid = new Grid { Padding = new Thickness(12, 8, 12, 8), Height = 48 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var img = new Microsoft.UI.Xaml.Controls.Image { Width = 24, Height = 24, Stretch = Stretch.Uniform };
            Microsoft.UI.Xaml.Controls.Grid.SetColumn(img, 0);
            var tb = new TextBlock { FontSize = 16, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            Microsoft.UI.Xaml.Controls.Grid.SetColumn(tb, 1);

            grid.Children.Add(img);
            grid.Children.Add(tb);

            grid.DataContextChanged += (s, e) =>
            {
                if (grid.DataContext is BowTab tab)
                {
                    img.Source = Uri.TryCreate(tab.FaviconUrl, UriKind.Absolute, out var uri)
                        ? new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(uri) : null;
                    tb.Text = tab.Title;
                }
            };

            grid.DataContext = args.Data;
            return grid;
        }
        public void RecycleElement(Microsoft.UI.Xaml.ElementFactoryRecycleArgs args) { }
    }

    public void Initialize(BowStore store)
    {
        _store = store;
        store.Tabs.CollectionChanged += (_, _) => Refresh();
    }

    private void Refresh()
    {
        if (_store is null) return;
        TabListView.Children.Clear();
        foreach (var tab in _store.Tabs)
        {
            var el = (Microsoft.UI.Xaml.FrameworkElement)new TabSwitcherElementFactory().GetElement(new Microsoft.UI.Xaml.ElementFactoryGetArgs { Data = tab });
            el.PointerPressed += (_, _) =>
            {
                _store.SetActiveTab(tab.Id);
                RequestClose?.Invoke();
            };
            TabListView.Children.Add(el);
        }
    }

    public new Visibility Visibility
    {
        get => base.Visibility;
        set
        {
            base.Visibility = value;
            if (value == Visibility.Visible)
            {
                Refresh();
            }
        }
    }
}
