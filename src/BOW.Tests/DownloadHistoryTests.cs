using BOW.Services;

namespace BOW.Tests;

public class DownloadHistoryTests
{
    [Fact]
    public void DownloadStatusAndFailureReasonSurviveRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), "bow-download-test-" + Guid.NewGuid());
        var path = Path.Combine(directory, "downloads.json");
        try
        {
            var failed = new DownloadRecord(Guid.NewGuid(), "https://example.org/file.zip",
                "file.zip", Path.Combine(directory, "file.zip"), DateTimeOffset.UtcNow,
                DownloadState.Failed, 512, 1024, "The network connection was lost.");
            DownloadHistoryStore.Save(path, [failed]);

            var restored = Assert.Single(DownloadHistoryStore.Load(path));
            Assert.Equal(failed.Id, restored.Id);
            Assert.Equal(DownloadState.Failed, restored.State);
            Assert.Equal("The network connection was lost.", restored.FailureReason);
            Assert.Equal(failed.LocalPath, restored.LocalPath);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
