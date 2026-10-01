using BOW.Services;
using System.Net;

namespace BOW.Tests;

public class ReleaseUpdateTests
{
    private sealed class ResponseHandler(string json, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        internal Uri? Requested;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requested = request.RequestUri;
            Assert.Null(request.Headers.Authorization);
            Assert.Equal("api.github.com", request.RequestUri!.Host);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json) });
        }
    }

    [Theory]
    [InlineData("0.1.0-preview.2", "0.1.0-preview.10", true)]
    [InlineData("0.1.0-preview.10", "0.1.0-preview.2", false)]
    [InlineData("0.1.0-preview.10", "0.1.0", true)]
    [InlineData("0.1.0+abc", "0.1.0+xyz", false)]
    public async Task ComparesSemanticVersions(string current, string released, bool newer)
    {
        var handler = new ResponseHandler($"[{{\"draft\":false,\"prerelease\":true,\"tag_name\":\"v{released}\"}}]");
        using var client = new HttpClient(handler);
        var result = await new ReleaseUpdateService(client).CheckAsync(current, "Preview");
        Assert.Equal(newer, result.IsNewer);
        Assert.StartsWith("https://github.com/alvyn16/BOW/releases/tag/", result.ReleasePage!.AbsoluteUri);
    }

    [Fact]
    public async Task StableChannelWithoutReleaseIsNotAnError()
    {
        var handler = new ResponseHandler("{}", HttpStatusCode.NotFound);
        using var client = new HttpClient(handler);
        var result = await new ReleaseUpdateService(client).CheckAsync("0.1.0-preview.2", "Stable");
        Assert.Null(result.AvailableVersion); Assert.False(result.IsNewer);
        Assert.EndsWith("/releases/latest", handler.Requested!.AbsoluteUri);
    }

    [Fact]
    public async Task IgnoresDraftsAndUnversionedTagsAndDoesNotTrustRemoteLinks()
    {
        using var client = new HttpClient(new ResponseHandler("""
            [{"draft":true,"prerelease":false,"tag_name":"v99.0.0"},
             {"draft":false,"prerelease":false,"tag_name":"banana"},
             {"draft":false,"prerelease":false,"tag_name":"v1.0.0","html_url":"https://evil.invalid"}]
            """));
        var result = await new ReleaseUpdateService(client).CheckAsync("0.1.0", "Preview");
        Assert.Equal("1.0.0", result.AvailableVersion);
        Assert.Equal("github.com", result.ReleasePage!.Host);
    }

    [Fact]
    public async Task MalformedResponseIsNotReportedAsUpToDate()
    {
        using var client = new HttpClient(new ResponseHandler("{}"));
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => new ReleaseUpdateService(client).CheckAsync("0.1.0", "Preview"));
    }

    [Fact]
    public async Task NetworkFailureIsNotReportedAsUpToDate()
    {
        using var client = new HttpClient(new ResponseHandler("{}", HttpStatusCode.Forbidden));
        await Assert.ThrowsAsync<HttpRequestException>(() => new ReleaseUpdateService(client).CheckAsync("0.1.0", "Stable"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"draft\":false,\"prerelease\":false}")]
    public async Task MalformedStableReleaseIsAnError(string json)
    {
        using var client = new HttpClient(new ResponseHandler(json));
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => new ReleaseUpdateService(client).CheckAsync("0.1.0", "Stable"));
    }

    [Fact]
    public async Task SelectsHighestVersionEvenWhenReleaseOrderDiffers()
    {
        using var client = new HttpClient(new ResponseHandler("""
            [{"draft":false,"prerelease":true,"tag_name":"v1.0.0-preview.10"},
             {"draft":false,"prerelease":true,"tag_name":"v1.0.0-preview.2"},
             {"draft":false,"prerelease":false,"tag_name":"v1.0.0"}]
            """));
        var result = await new ReleaseUpdateService(client).CheckAsync("1.0.0-preview.10", "Preview");
        Assert.Equal("1.0.0", result.AvailableVersion);
        Assert.True(result.IsNewer);
    }

    [Fact]
    public async Task StableChannelExcludesPrereleaseVersionRegardlessOfFlag()
    {
        using var client = new HttpClient(new ResponseHandler("""
            {"draft":false,"prerelease":false,"tag_name":"v2.0.0-preview.1"}
            """));
        var result = await new ReleaseUpdateService(client).CheckAsync("1.0.0", "Stable");
        Assert.Null(result.AvailableVersion);
    }
}
