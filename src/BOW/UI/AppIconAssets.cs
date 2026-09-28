using Microsoft.UI.Xaml.Media.Imaging;

namespace BOW.UI;

internal static class AppIconAssets
{
    public static string Normalize(string? variant) =>
        string.Equals(variant, "Black", StringComparison.OrdinalIgnoreCase)
            ? "Black" : "White";

    public static string IconPath(string? variant) => Path.Combine(
        AppContext.BaseDirectory, "Assets", "Icons",
        $"brow-icon-{Normalize(variant).ToLowerInvariant()}.ico");

    public static BitmapImage Preview(string? variant) => new(new Uri(Path.Combine(
        AppContext.BaseDirectory, "Assets", "Icons",
        $"brow-icon-{Normalize(variant).ToLowerInvariant()}.png")));
}
