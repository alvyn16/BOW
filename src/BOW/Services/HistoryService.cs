using System.Text.Json;

namespace BOW.Services;

public sealed record HistoryEntry(string Url, string Title, string? FaviconUrl, DateTimeOffset VisitedAt);

/// <summary>Recent successful page visits, saved between browser sessions.</summary>
public sealed class HistoryService
{
    public static HistoryService Instance { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BOW", "history.json"));

    private const int MaxEntries = 200;
    private readonly string _path;
    private readonly List<HistoryEntry> _entries;

    public HistoryService(string path)
    {
        _path = path;
        try
        {
            var entries = File.Exists(path)
                ? JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(path)) ?? []
                : [];
            _entries = entries.Select(entry => entry with
            {
                Url = PersistedUrl.Sanitize(entry.Url),
                FaviconUrl = entry.FaviconUrl is null ? null : PersistedUrl.Sanitize(entry.FaviconUrl)
            }).ToList();
            if (!entries.SequenceEqual(_entries)) Save();
        }
        catch (Exception)
        {
            _entries = [];
        }
    }

    public IReadOnlyList<HistoryEntry> Recent(int count = 5) =>
        _entries.Take(Math.Max(0, count)).ToArray();

    public IReadOnlyList<HistoryEntry> Search(string query, int count = 5)
    {
        var matches = new List<HistoryEntry>(Math.Max(0, count));
        if (count <= 0) return matches;
        query = query.Trim();
        foreach (var entry in _entries)
        {
            if (query.Length > 0
                && !entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !entry.Url.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
            matches.Add(entry);
            if (matches.Count == count) break;
        }
        return matches;
    }

    public void RecordVisit(string url, string? title, string? faviconUrl)
    {
        if (!IsWebPage(url)) return;
        url = PersistedUrl.Sanitize(url);
        _entries.RemoveAll(entry => string.Equals(entry.Url, url, StringComparison.OrdinalIgnoreCase));
        _entries.Insert(0, new HistoryEntry(url, DisplayTitle(url, title),
            faviconUrl is null ? null : PersistedUrl.Sanitize(faviconUrl), DateTimeOffset.UtcNow));
        if (_entries.Count > MaxEntries) _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
        Save();
    }

    public void UpdateDetails(string url, string? title, string? faviconUrl)
    {
        url = PersistedUrl.Sanitize(url);
        var index = _entries.FindIndex(entry => string.Equals(entry.Url, url, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return;
        var current = _entries[index];
        _entries[index] = current with
        {
            Title = DisplayTitle(url, title),
            FaviconUrl = string.IsNullOrWhiteSpace(faviconUrl) ? current.FaviconUrl : PersistedUrl.Sanitize(faviconUrl)
        };
        Save();
    }

    private static bool IsWebPage(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string DisplayTitle(string url, string? title) =>
        string.IsNullOrWhiteSpace(title) || title == "New Tab"
            ? new Uri(url).Host
            : title;

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(_entries));
            File.Move(temp, _path, true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not save history: {ex.Message}");
        }
    }

    public void Clear()
    {
        if (File.Exists(_path)) File.Delete(_path);
        _entries.Clear();
    }

    public void ClearSince(DateTimeOffset? since)
    {
        if (since is null)
        {
            Clear();
            return;
        }
        var remaining = _entries.Where(entry => entry.VisitedAt < since.Value).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(remaining));
        File.Move(temp, _path, true);
        _entries.Clear();
        _entries.AddRange(remaining);
    }
}
