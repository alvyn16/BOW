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
            return File.Exists(path)
                ? JsonSerializer.Deserialize<List<DownloadRecord>>(File.ReadAllText(path)) ?? []
                : [];
        }
        catch { return []; }
    }

    public static void Save(string path, IEnumerable<DownloadRecord> records)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(records.Take(200).ToArray()));
        File.Move(temporaryPath, path, true);
    }
}
