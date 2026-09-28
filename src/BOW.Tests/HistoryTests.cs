using BOW.Services;

namespace BOW.Tests;

public class HistoryTests
{
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
