using BOW.Core;
using BOW.UI.WebView;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BOW.UI.SplitTabView;

public sealed class SplitTabHost : UserControl
{
    public WebViewHost PrimaryHost { get; }
    public WebViewHost SecondaryHost { get; }

    public SplitTabHost()
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        PrimaryHost = new WebViewHost();
        Grid.SetColumn(PrimaryHost, 0);
        root.Children.Add(PrimaryHost);

        var border = new Border { Width = 1, Background = ThemeBrushes.TopBarBorderBrush };
        Grid.SetColumn(border, 1);
        root.Children.Add(border);

        SecondaryHost = new WebViewHost();
        Grid.SetColumn(SecondaryHost, 2);
        root.Children.Add(SecondaryHost);

        this.Content = root;
    }

    public void SetTabs(BowTab primary, BowTab? secondary)
    {
        PrimaryHost.SetTab(primary);
        if (secondary is not null)
            SecondaryHost.SetTab(secondary);
    }
}
