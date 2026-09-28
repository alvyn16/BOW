namespace BOW.Services;

/// <summary>
/// Resolves an omnibar input string to a navigable URL.
/// </summary>
public static class SearchService
{
    /// <summary>
    /// Resolves <paramref name="input"/> to a URL.
    /// Rules:
    ///   - Contains a space → treat as search query
    ///   - Already has a scheme (http/https/ftp/file) → use as-is
    ///   - Looks like a hostname (contains dot, or is "localhost") → prepend https://
    ///   - Everything else → search query
    /// </summary>
    public static string Resolve(string input, BOW.Core.SettingsModel settings)
    {
        input = input.Trim();

        if (string.IsNullOrEmpty(input))
            return "bow:newtab";

        // Already has a scheme
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri) &&
            (uri.Scheme == "http" || uri.Scheme == "https" ||
             uri.Scheme == "ftp" || uri.Scheme == "file" || uri.Scheme == "bow"))
            return input;

        // Has a space → search
        if (input.Contains(' '))
            return BuildSearchUrl(input, settings);

        // Looks like a URL: has a dot or is localhost[:port]
        var host = input.Split(':')[0];
        if (host.Contains('.') || host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            // localhost gets http, everything else gets https
            var scheme = host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ? "http" : "https";
            return $"{scheme}://{input}";
        }

        // Fallback: search
        return BuildSearchUrl(input, settings);
    }

    private static string BuildSearchUrl(string query, BOW.Core.SettingsModel settings)
    {
        var encoded = Uri.EscapeDataString(query);

        if (settings.SearchEngine == "Custom" &&
            settings.CustomSearchUrl.Contains("{0}", StringComparison.Ordinal) &&
            Uri.TryCreate(settings.CustomSearchUrl.Replace("{0}", encoded), UriKind.Absolute, out var customUri) &&
            customUri.Scheme is "http" or "https")
            return customUri.AbsoluteUri;

        var template = settings.SearchEngine switch
        {
            "Startpage" => "https://www.startpage.com/sp/search?query={0}",
            "Google" => "https://www.google.com/search?q={0}",
            "Bing" => "https://www.bing.com/search?q={0}", // Existing saved preference.
            _ => "https://duckduckgo.com/?q={0}"
        };
        return string.Format(template, encoded);
    }
}
