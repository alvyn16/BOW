using System.Text.Json;

namespace BOW.Services;

/// <summary>Entry saved per tab in the session file.</summary>
public record SessionEntry(string Url, bool IsPinned, bool IsActive = false,
    string? GroupName = null, bool IsMuted = false);

/// <summary>
/// Saves and restores the browser session from %LOCALAPPDATA%\BOW\session.json.
/// </summary>
public static class SessionManager
{
    private static readonly string _sessionPath =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BOW", "session.json");

    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    /// <summary>Saves all tabs, including sleeping and blank tabs. Atomic write.</summary>
    public static void Save(IEnumerable<BOW.Core.BowTab> tabs, Guid? activeTabId = null) =>
        SaveTo(_sessionPath, tabs, activeTabId);

    public static void SaveTo(string path, IEnumerable<BOW.Core.BowTab> tabs, Guid? activeTabId = null)
    {
        try
        {
            var entries = tabs
                .Where(t => !string.IsNullOrEmpty(t.Url))
                .Select(t => new SessionEntry(PersistedUrl.Sanitize(t.Url), t.IsPinned, t.Id == activeTabId,
                    t.GroupName, t.IsMuted))
                .ToList();

            var dir = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(dir);

            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(entries, _jsonOptions));
            File.Move(tmp, path, overwrite: true);
        }
        catch { /* never crash on save */ }
    }

    /// <summary>Loads the saved session. Returns empty list if missing or corrupt.</summary>
    public static IReadOnlyList<SessionEntry> Load() => LoadFrom(_sessionPath);

    public static IReadOnlyList<SessionEntry> LoadFrom(string path)
    {
        try
        {
            if (!File.Exists(path))
                return [];

            var json = File.ReadAllText(path);
            var entries = JsonSerializer.Deserialize<List<SessionEntry>>(json) ?? [];
            var sanitized = entries.Select(entry => entry with { Url = PersistedUrl.Sanitize(entry.Url) }).ToList();
            if (!entries.SequenceEqual(sanitized))
            {
                try
                {
                    var tmp = path + ".tmp";
                    File.WriteAllText(tmp, JsonSerializer.Serialize(sanitized, _jsonOptions));
                    File.Move(tmp, path, overwrite: true);
                }
                catch { /* still restore sanitized tabs if migration cannot be written */ }
            }
            return sanitized;
        }
        catch
        {
            return [];
        }
    }
}
