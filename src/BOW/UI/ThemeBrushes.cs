using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls;

namespace BOW.UI;

public static class ThemeBrushes
{
    // Zen Browser uses Segoe UI for its Windows interface. Keep Fluent Icons separate.
    public static FontFamily UiFont { get; } = new("Segoe UI");
    public static bool IsDark { get; private set; }
    public static SolidColorBrush WindowBackgroundBrush { get; } = new(Color(255, 255, 255));
    public static SolidColorBrush TopBarBrush { get; } = new(Color(239, 239, 243));
    public static SolidColorBrush TopBarBorderBrush { get; } = new(Color(222, 222, 225));
    public static SolidColorBrush SidebarBrush { get; } = new(Color(247, 247, 248));
    public static SolidColorBrush TextBrush { get; } = new(Color(34, 34, 36));
    public static SolidColorBrush MutedTextBrush { get; } = new(Color(129, 129, 134));
    public static SolidColorBrush AccentBrush { get; } = new(Color(52, 193, 94));
    public static SolidColorBrush TabActiveBrush { get; } = new(Color(255, 255, 255));
    public static SolidColorBrush ControlSurfaceBrush { get; } = new(Color(251, 251, 252));
    public static SolidColorBrush SelectedBrush { get; } = new(Color(229, 229, 231));
    public static SolidColorBrush SwitchTrackBrush { get; } = new(Color(29, 29, 31));

    public static void StyleTextBox(TextBox textBox)
    {
        textBox.Background = ControlSurfaceBrush;
        textBox.Foreground = TextBrush;
        textBox.Resources["TextControlBackground"] = ControlSurfaceBrush;
        textBox.Resources["TextControlBackgroundPointerOver"] = ControlSurfaceBrush;
        textBox.Resources["TextControlBackgroundFocused"] = ControlSurfaceBrush;
        textBox.Resources["TextControlForeground"] = TextBrush;
        textBox.Resources["TextControlForegroundPointerOver"] = TextBrush;
        textBox.Resources["TextControlForegroundFocused"] = TextBrush;
        textBox.Resources["TextControlPlaceholderForeground"] = MutedTextBrush;
        textBox.Resources["TextControlPlaceholderForegroundPointerOver"] = MutedTextBrush;
        textBox.Resources["TextControlPlaceholderForegroundFocused"] = MutedTextBrush;
    }

    public static void Apply(string theme)
    {
        IsDark = theme == "Dark" ||
            (theme == "Auto" && new Windows.UI.ViewManagement.UISettings()
                .GetColorValue(Windows.UI.ViewManagement.UIColorType.Background).R < 128);
        WindowBackgroundBrush.Color = IsDark ? Color(23, 23, 25) : Color(255, 255, 255);
        TopBarBrush.Color = IsDark ? Color(37, 37, 41) : Color(239, 239, 243);
        TopBarBorderBrush.Color = IsDark ? Color(64, 64, 69) : Color(222, 222, 225);
        SidebarBrush.Color = IsDark ? Color(31, 31, 34) : Color(247, 247, 248);
        TextBrush.Color = IsDark ? Color(242, 242, 243) : Color(34, 34, 36);
        MutedTextBrush.Color = IsDark ? Color(158, 158, 164) : Color(129, 129, 134);
        TabActiveBrush.Color = IsDark ? Color(53, 53, 58) : Color(255, 255, 255);
        ControlSurfaceBrush.Color = IsDark ? Color(43, 43, 47) : Color(251, 251, 252);
        SelectedBrush.Color = IsDark ? Color(64, 64, 69) : Color(229, 229, 231);
        SwitchTrackBrush.Color = IsDark ? Color(242, 242, 243) : Color(29, 29, 31);
    }

    private static Windows.UI.Color Color(byte r, byte g, byte b) =>
        Windows.UI.Color.FromArgb(255, r, g, b);
}
