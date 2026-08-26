using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace ServerBrowser.Feeds;

public static class ServerPingProbe {
    public static async Task PopulateAsync(
        IReadOnlyList<ServerListing> servers,
        TimeSpan timeout,
        int maxConcurrency,
        CancellationToken cancellationToken = default) {
        using var gate = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var probes = servers.Select(async server => {
            await gate.WaitAsync(cancellationToken);
            try {
                server.PingMs = await MeasureAsync(
                    server.Host,
                    timeout,
                    cancellationToken);
            }
            finally {
                gate.Release();
            }
        });
        await Task.WhenAll(probes);
    }

    public static async Task<int?> MeasureAsync(
        string host,
        TimeSpan timeout,
        CancellationToken cancellationToken = default) {
        using var ping = new Ping();
        try {
            var reply = await ping.SendPingAsync(host, (int)timeout.TotalMilliseconds)
                .WaitAsync(cancellationToken);
            return reply.Status == IPStatus.Success ? (int)reply.RoundtripTime : null;
        }
        catch (Exception ex) when (ex is PingException or OperationCanceledException) {
            return null;
        }
    }
}
