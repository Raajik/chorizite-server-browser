using Xunit;

namespace ServerBrowser.Tests;

public class DiscordLinkTests {
    [Theory]
    [InlineData("https://discord.gg/example")]
    [InlineData("https://DISCORD.GG/example")]
    public void IsSupportedAcceptsDiscordHttpsInvites(string url) {
        Assert.True(DiscordLink.IsSupported(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://discord.gg/example")]
    [InlineData("https://discord.gg.evil.example/invite")]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("steam://discord.gg/example")]
    public void IsSupportedRejectsUnsafeOrUnrelatedLinks(string? url) {
        Assert.False(DiscordLink.IsSupported(url));
    }
}