using BOW.Core;

namespace BOW.Tests;

public class BrowsingSafetyTests
{
    [Theory]
    [InlineData("mailto:person@example.org", true, true)]
    [InlineData("steam://open", true, true)]
    [InlineData("mailto:person@example.org", false, false)]
    [InlineData("javascript:alert(1)", true, false)]
    [InlineData("file:///C:/secret.txt", true, false)]
    [InlineData("https://example.org", true, false)]
    public void ExternalLaunchRequiresSafeSchemeAndUserAction(string uri, bool initiated, bool expected) =>
        Assert.Equal(expected, BrowsingSafety.CanOfferExternalLaunch(uri, initiated));

    [Theory]
    [InlineData("setup.exe", true)]
    [InlineData("INSTALL.MSI", true)]
    [InlineData("readme.pdf", false)]
    [InlineData("photo.png", false)]
    public void ExecutableDownloadsNeedConfirmation(string fileName, bool expected) =>
        Assert.Equal(expected, BrowsingSafety.NeedsDownloadConfirmation(fileName));

    [Fact]
    public void FailedHttpsPageDoesNotClaimEncryption() =>
        Assert.StartsWith("This page failed", BrowsingSafety.ConnectionDescription(
            "https://example.org", loaded: false, failed: true));
}
