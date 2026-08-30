using ServerBrowser.Feeds;
using Xunit;

namespace ServerBrowser.Tests;

public class ServerPingProbeTests {
    [Fact]
    public async Task MeasureAsyncReturnsIcmpLatencyForReachableHost() {
        var result = await ServerPingProbe.MeasureAsync(
            "127.0.0.1",
            port: 9000,
            TimeSpan.FromSeconds(1));

        Assert.True(result.HostResolved);
        Assert.NotNull(result.LatencyMs);
        Assert.InRange(result.LatencyMs.Value, 0, 1000);
    }

    [Fact]
    public async Task MeasureAsyncReportsUnresolvableHostsSeparatelyFromSilentOnes() {
        var result = await ServerPingProbe.MeasureAsync(
            "server-browser-no-such-host.invalid",
            port: 9000,
            TimeSpan.FromSeconds(1));

        Assert.False(result.HostResolved);
        Assert.Null(result.LatencyMs);
    }

    [Fact]
    public async Task PopulateAsyncStoresLatencyOnEachReachableListing() {
        var server = new ServerListing {
            Host = "127.0.0.1",
            Port = 9000
        };

        await ServerPingProbe.PopulateAsync(
            [server],
            TimeSpan.FromSeconds(1),
            maxConcurrency: 1);

        Assert.True(server.HostResolved);
        Assert.NotNull(server.PingMs);
    }

    [Fact]
    public async Task PopulateAsyncMarksUnresolvableListingsAsOffline() {
        var server = new ServerListing {
            Host = "server-browser-no-such-host.invalid",
            Port = 9000
        };

        await ServerPingProbe.PopulateAsync(
            [server],
            TimeSpan.FromSeconds(1),
            maxConcurrency: 1);

        Assert.False(server.HostResolved);
        Assert.Null(server.PingMs);
    }
}
