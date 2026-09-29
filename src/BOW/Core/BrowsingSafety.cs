namespace BOW.Core;

public static class BrowsingSafety
{
    private static readonly HashSet<string> BlockedExternalSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "about", "blob", "bow", "data", "file", "javascript", "ms-appdata", "ms-appx"
    };

    private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bat", ".cmd", ".com", ".exe", ".hta", ".js", ".jse", ".msi",
        ".msix", ".ps1", ".reg", ".scr", ".vbe", ".vbs", ".wsf"
    };

    public static bool IsWebAddress(string? address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https";

    public static bool CanOfferExternalLaunch(string? address, bool isUserInitiated) =>
        isUserInitiated
        && Uri.TryCreate(address, UriKind.Absolute, out var uri)
        && uri.Scheme is not ("http" or "https")
        && !BlockedExternalSchemes.Contains(uri.Scheme);

    public static bool NeedsDownloadConfirmation(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && ExecutableExtensions.Contains(Path.GetExtension(fileName));

    public static string ConnectionDescription(string? address, bool loaded, bool failed)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)) return "No site is open.";
        if (failed) return "This page failed to load. Check the page warning before entering information.";
        if (uri.Scheme == "http") return "Not secure. Information sent to this site may be visible to others.";
        if (uri.Scheme == "https") return loaded
            ? "HTTPS encrypts this connection. It does not guarantee that this site is trustworthy."
            : "Checking this connection…";
        return "This page does not use an HTTP or HTTPS connection.";
    }
}
