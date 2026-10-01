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
    private static readonly object _gate = new();

    /// <summary>Saves all tabs, including sleeping and blank tabs. Atomic write.</summary>
    public static void Save(IEnumerable<BOW.Core.BowTab> tabs, Guid? activeTabId = null) =>
        SaveTo(_sessionPath, tabs, activeTabId);

    public static void SaveTo(string path, IEnumerable<BOW.Core.BowTab> tabs, Guid? activeTabId = null)
    {
        lock (_gate) try
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
            // Rotate only a fully valid session; never replace a good backup with damage.
            if (TryRead(path, out _, out var complete) && complete)
                File.Replace(tmp, path, path + ".bak");
            else
                File.Move(tmp, path, overwrite: true);
        }
        catch { /* never crash on save */ }
    }

    /// <summary>Restores the current session, or its last valid backup if damaged.</summary>
    public static IReadOnlyList<SessionEntry> Load() => LoadFrom(_sessionPath);

    public static IReadOnlyList<SessionEntry> LoadFrom(string path)
    {
        lock (_gate)
        {
            var currentReadable = TryRead(path, out var current, out var complete);
            if (currentReadable && complete) { RewriteSanitized(path, current); return current; }
            if (TryRead(path + ".bak", out var backup, out var backupComplete) && backupComplete)
            { RewriteSanitized(path + ".bak", backup); return backup; }
            return currentReadable ? current : [];
        }
    }

    private static bool TryRead(string path, out List<SessionEntry> entries, out bool complete)
    {
        entries = [];
        complete = false;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Array) return false;
            complete = true;
            foreach (var element in document.RootElement.EnumerateArray())
            {
                try
                {
                    var entry = element.Deserialize<SessionEntry>();
                    if (entry is null || string.IsNullOrWhiteSpace(entry.Url)) { complete = false; continue; }
                    entries.Add(entry with { Url = PersistedUrl.Sanitize(entry.Url) });
                }
                catch (JsonException) { complete = false; }
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        { return false; }
    }

    private static void RewriteSanitized(string path, List<SessionEntry> entries)
    {
        try
        {
            var json = JsonSerializer.Serialize(entries, _jsonOptions);
            if (File.ReadAllText(path) == json) return;
            File.WriteAllText(path + ".tmp", json);
            File.Move(path + ".tmp", path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { /* Recovery must still work on read-only storage. */ }
    }
}
