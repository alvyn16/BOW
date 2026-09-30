using BOW.Services;

namespace BOW.Tests;

public class TabSleepPolicyTests
{
    [Theory]
    [InlineData("https://example.com/page", true)]
    [InlineData("https://EXAMPLE.com/page", true)]
    [InlineData("https://sub.example.com/page", false)]
    [InlineData("https://notexample.com/page", false)]
    [InlineData("bow:newtab", false)]
    public void ExceptionsMatchOnlyTheExactWebHost(string url, bool expected)
    {
        Assert.Equal(expected, TabSleepPolicy.IsExcluded(url, ["example.com"]));
    }
}
