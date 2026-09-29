using System.Text.Json;

namespace BOW.Services;

public enum DownloadState { InProgress, Paused, Completed, Cancelled, Failed }

public sealed record DownloadRecord(
    Guid Id, string Uri, string FileName, string LocalPath, DateTimeOffset StartedAt,
    DownloadState State, long BytesReceived, long TotalBytes, string? FailureReason);

/// <summary>Saved download history; active operations are never serialized.</summary>
public static class DownloadHistoryStore
{
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BOW", "downloads.json");

    public static IReadOnlyList<DownloadRecord> Load(string path)
    {
        try
        {
            var records = File.Exists(path)
                ? JsonSerializer.Deserialize<List<DownloadRecord>>(File.ReadAllText(path)) ?? []
                : [];
            var sanitized = records.Select(record => record with
            {
                Uri = PersistedUrl.Sanitize(record.Uri)
            }).ToArray();
            if (!records.SequenceEqual(sanitized))
            {
                try { Save(path, sanitized); }
                catch { /* still show sanitized records if migration cannot be written */ }
            }
            return sanitized;
        }
        catch { return []; }
    }

    public static void Save(string path, IEnumerable<DownloadRecord> records)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(records.Take(200)
            .Select(record => record with { Uri = PersistedUrl.Sanitize(record.Uri) }).ToArray()));
        File.Move(temporaryPath, path, true);
    }
}
