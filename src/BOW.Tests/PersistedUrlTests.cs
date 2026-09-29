using BOW.Services;

namespace BOW.Tests;

public class PersistedUrlTests
{
    [Theory]
    [InlineData("https://user:pass@example.org/path?x=1&access_token=secret#section", "https://example.org/path?x=1#section")]
    [InlineData("https://example.org/callback?code=abc&next=%2Fhome#access_token=xyz", "https://example.org/callback?next=%2Fhome")]
    [InlineData("https://example.org/?access-Token=abc&id=42", "https://example.org/?id=42")]
    [InlineData("https://example.org/page?id=42#/details", "https://example.org/page?id=42#/details")]
    public void RemovesCredentialLikeUrlParts(string input, string expected) =>
        Assert.Equal(expected, PersistedUrl.Sanitize(input));

    [Fact]
    public void ExistingSavedUrlsAreSanitizedWhenLoaded()
    {
        var directory = Path.Combine(Path.GetTempPath(), "bow-url-test-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var unsafeUrl = "https://example.org/callback?code=secret&next=home";
        try
        {
            var sessionPath = Path.Combine(directory, "session.json");
            File.WriteAllText(sessionPath, System.Text.Json.JsonSerializer.Serialize(
                new[] { new SessionEntry(unsafeUrl, false) }));
            Assert.Equal("https://example.org/callback?next=home", SessionManager.LoadFrom(sessionPath)[0].Url);
            Assert.DoesNotContain("secret", File.ReadAllText(sessionPath));

            var historyPath = Path.Combine(directory, "history.json");
            File.WriteAllText(historyPath, System.Text.Json.JsonSerializer.Serialize(
                new[] { new HistoryEntry(unsafeUrl, "Callback", null, DateTimeOffset.UtcNow) }));
            var history = new HistoryService(historyPath);
            Assert.DoesNotContain("secret", File.ReadAllText(historyPath));
            history.Clear();
            Assert.Empty(new HistoryService(historyPath).Recent());

            var downloadsPath = Path.Combine(directory, "downloads.json");
            File.WriteAllText(downloadsPath, System.Text.Json.JsonSerializer.Serialize(new[]
            {
                new DownloadRecord(Guid.NewGuid(), unsafeUrl, "file.txt", "file.txt",
                    DateTimeOffset.UtcNow, DownloadState.Completed, 1, 1, null)
            }));
            Assert.DoesNotContain("secret", DownloadHistoryStore.Load(downloadsPath)[0].Uri);
            Assert.DoesNotContain("secret", File.ReadAllText(downloadsPath));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
