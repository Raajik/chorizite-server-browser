using Xunit;

namespace ServerBrowser.Tests;

public class WebsiteLinkTests {
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com/servers")]
    [InlineData("HTTPS://Example.COM/path?q=1")]
    public void IsSupportedAcceptsWebLinks(string url) {
        Assert.True(WebsiteLink.IsSupported(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("example.com")]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("steam://run/1234")]
    public void IsSupportedRejectsNonWebLinks(string? url) {
        Assert.False(WebsiteLink.IsSupported(url));
    }

    [Fact]
    public void TryOpenReportsUnsupportedLinksWithoutLaunching() {
        Assert.False(WebsiteLink.TryOpen("file:///C:/Windows/System32/calc.exe", out var error));
        Assert.Equal("Only http and https website links can be opened", error);
    }
}
