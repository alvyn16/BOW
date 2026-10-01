using BOW.Core;
using BOW.Services;

namespace BOW.Tests;

public sealed class SessionRecoveryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "bow-recovery-" + Guid.NewGuid());
    private string PathName => Path.Combine(_directory, "session.json");
    public SessionRecoveryTests() => Directory.CreateDirectory(_directory);
    private void Save(string url) => SessionManager.SaveTo(PathName, new[] { new BowTab { Url = url } });

    [Fact]
    public void CorruptCurrentRestoresPreviousAndNextSavePreservesBackup()
    {
        Save("https://example.org/first");
        Save("https://example.org/second");
        File.WriteAllText(PathName, "{broken");
        Assert.Equal("https://example.org/first", Assert.Single(SessionManager.LoadFrom(PathName)).Url);
        Save("https://example.org/third");
        Assert.Equal("https://example.org/first", Assert.Single(SessionManager.LoadFrom(PathName + ".bak")).Url);
    }

    [Fact]
    public void MissingCurrentUsesBackup()
    {
        Save("about:blank"); Save("https://example.org"); File.Delete(PathName);
        Assert.Equal("about:blank", Assert.Single(SessionManager.LoadFrom(PathName)).Url);
    }

    [Fact]
    public void PartialFileSalvagesValidEntriesWithoutBackup()
    {
        File.WriteAllText(PathName, "[{\"Url\":\"https://example.org?code=secret\",\"IsPinned\":true},null,{\"Url\":42}]");
        var entry = Assert.Single(SessionManager.LoadFrom(PathName));
        Assert.Equal("https://example.org/", entry.Url);
        Assert.True(entry.IsPinned);
    }

    [Fact]
    public void EmptySessionIsValidAndDoesNotResurrectClosedTabs()
    {
        Save("https://example.org"); SessionManager.SaveTo(PathName, Array.Empty<BowTab>());
        Assert.Empty(SessionManager.LoadFrom(PathName));
    }

    [Fact]
    public void BothDamagedFilesReturnEmptyWithoutThrowing()
    {
        File.WriteAllText(PathName, "null"); File.WriteAllText(PathName + ".bak", "garbage");
        Assert.Empty(SessionManager.LoadFrom(PathName));
    }

    public void Dispose() => Directory.Delete(_directory, true);
}
