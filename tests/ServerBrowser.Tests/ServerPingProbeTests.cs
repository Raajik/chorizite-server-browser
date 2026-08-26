using ServerBrowser.Feeds;
using Xunit;

namespace ServerBrowser.Tests;

public class ServerPingProbeTests {
    [Fact]
    public async Task MeasureAsyncReturnsIcmpLatencyForReachableHost() {
        var latency = await ServerPingProbe.MeasureAsync(
            "127.0.0.1",
            TimeSpan.FromSeconds(1));

        Assert.NotNull(latency);
        Assert.InRange(latency.Value, 0, 1000);
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

        Assert.NotNull(server.PingMs);
    }
}
