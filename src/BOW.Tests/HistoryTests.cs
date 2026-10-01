using BOW.Services;

namespace BOW.Tests;

public class HistoryTests
{
    [Fact]
    public void ClearSinceKeepsOlderVisits()
    {
        var path = Path.Combine(Path.GetTempPath(), "bow-history-test-" + Guid.NewGuid(), "history.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var old = new HistoryEntry("https://example.org/old", "Old", null,
                DateTimeOffset.UtcNow.AddDays(-10));
            var recent = new HistoryEntry("https://example.org/new", "New", null,
                DateTimeOffset.UtcNow.AddMinutes(-10));
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new[] { recent, old }));

            var history = new HistoryService(path);
            history.ClearSince(DateTimeOffset.UtcNow.AddHours(-1));

            Assert.Equal(old.Url, Assert.Single(history.Recent()).Url);
            Assert.Equal(old.Url, Assert.Single(new HistoryService(path).Recent()).Url);
            history.ClearSince(null);
            Assert.Empty(new HistoryService(path).Recent());
        }
        finally
        {
            var directory = Path.GetDirectoryName(path)!;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void VisitsPersistInRecencyOrderWithoutDuplicateUrls()
    {
        var path = Path.Combine(Path.GetTempPath(), "bow-history-test-" + Guid.NewGuid(), "history.json");
        try
        {
            var history = new HistoryService(path);
            history.RecordVisit("https://example.org/one", "First", null);
            history.RecordVisit("https://example.org/two", "Second", null);
            history.RecordVisit("https://example.org/one", "Updated", null);
            history.RecordVisit("bow:newtab", "New Tab", null);

            var restored = new HistoryService(path).Recent();
            Assert.Equal(2, restored.Count);
            Assert.Equal("https://example.org/one", restored[0].Url);
            Assert.Equal("Updated", restored[0].Title);
            Assert.Equal("https://example.org/two", restored[1].Url);
        }
        finally
        {
            var directory = Path.GetDirectoryName(path)!;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
