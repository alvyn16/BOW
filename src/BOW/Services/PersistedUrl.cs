namespace BOW.Services;

/// <summary>Removes common credentials from URLs before they enter BOW's JSON files.</summary>
public static class PersistedUrl
{
    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "accesstoken", "idtoken", "refreshtoken", "authtoken", "token",
        "code", "state", "password", "passwd", "secret", "clientsecret",
        "apikey", "session", "sessionid", "signature", "sig", "jwt"
    };

    public static string Sanitize(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")) return url;

        var builder = new UriBuilder(uri) { UserName = string.Empty, Password = string.Empty };
        var query = uri.Query.TrimStart('?');
        builder.Query = string.Join("&", query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => !IsSensitiveKey(part.Split('=', 2)[0])));

        var fragment = uri.Fragment.TrimStart('#');
        if (fragment.Split('?', '&').Any(part => IsSensitiveKey(part.Split('=', 2)[0])))
            builder.Fragment = string.Empty;

        return builder.Uri.AbsoluteUri;
    }

    private static bool IsSensitiveKey(string key)
    {
        try
        {
            var normalized = Uri.UnescapeDataString(key.Replace('+', ' '))
                .Replace("_", string.Empty).Replace("-", string.Empty);
            return SensitiveKeys.Contains(normalized);
        }
        catch (UriFormatException) { return false; }
    }
}
