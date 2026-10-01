namespace BOW.Services;

public static class TabSleepPolicy
{
    public static bool IsExcluded(string url, IEnumerable<string>? hosts) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && (hosts?.Contains(uri.Host, StringComparer.OrdinalIgnoreCase) ?? false);
}
