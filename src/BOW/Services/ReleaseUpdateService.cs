using NuGet.Versioning;
using System.Net;
using System.Reflection;
using System.Text.Json;

namespace BOW.Services;

public sealed record ReleaseUpdate(string InstalledVersion, string? AvailableVersion, Uri? ReleasePage,
    bool IsNewer);

public sealed class ReleaseUpdateService
{
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 2 * 1024 * 1024
    };
    private readonly HttpClient _client;
    public ReleaseUpdateService(HttpClient? client = null) => _client = client ?? Client;
    public static string InstalledVersion => typeof(ReleaseUpdateService).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "0.0.0";

    public async Task<ReleaseUpdate> CheckAsync(string installedVersion, string channel,
        CancellationToken cancellationToken = default)
    {
        if (!NuGetVersion.TryParse(installedVersion, out var installed))
            throw new InvalidOperationException("The installed release version is invalid.");
        if (channel is not ("Stable" or "Preview")) throw new ArgumentException("Unknown update channel.", nameof(channel));
        var endpoint = channel == "Stable" ? "/latest" : "?per_page=100";
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "https://api.github.com/repos/alvyn16/BOW/releases" + endpoint);
        request.Headers.UserAgent.ParseAdd("BOW-UpdateCheck/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await _client.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound && channel == "Stable")
            return new(installedVersion, null, null, false);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (json.RootElement.ValueKind != (channel == "Stable" ? JsonValueKind.Object : JsonValueKind.Array))
            throw new JsonException("Unexpected release response format.");
        var releases = channel == "Stable" ? new[] { json.RootElement } : json.RootElement.EnumerateArray().ToArray();
        NuGetVersion? latest = null;
        string? tag = null;
        foreach (var release in releases)
        {
            if (release.ValueKind != JsonValueKind.Object) throw new JsonException("Invalid release entry.");
            if (!release.TryGetProperty("draft", out var draft) || draft.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new JsonException("Invalid release draft flag.");
            if (draft.GetBoolean()) continue;
            if (!release.TryGetProperty("prerelease", out var preview) ||
                preview.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new JsonException("Invalid release preview flag.");
            if (!release.TryGetProperty("tag_name", out var name) || name.ValueKind != JsonValueKind.String)
                throw new JsonException("Invalid release tag.");
            var candidateTag = name.GetString()!;
            var candidateVersion = candidateTag.StartsWith('v') ? candidateTag[1..] : candidateTag;
            if (!NuGetVersion.TryParse(candidateVersion, out var candidate)) continue;
            if (channel == "Stable" && (preview.GetBoolean() || candidate.IsPrerelease)) continue;
            if (latest is null || VersionComparer.VersionRelease.Compare(candidate, latest) > 0)
            { latest = candidate; tag = candidateTag; }
        }
        return new(installedVersion, latest?.ToFullString(), tag is null ? null : new Uri(
            "https://github.com/alvyn16/BOW/releases/tag/" + Uri.EscapeDataString(tag)),
            latest is not null && VersionComparer.VersionRelease.Compare(latest, installed) > 0);
    }
}
