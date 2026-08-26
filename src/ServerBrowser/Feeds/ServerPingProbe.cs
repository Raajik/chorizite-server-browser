using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ServerBrowser.Feeds;

public readonly record struct PingProbeResult(bool HostResolved, int? LatencyMs);

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
                var result = await MeasureAsync(server.Host, timeout, cancellationToken);
                server.HostResolved = result.HostResolved;
                server.PingMs = result.LatencyMs;
            }
            finally {
                gate.Release();
            }
        });
        await Task.WhenAll(probes);
    }

    public static async Task<PingProbeResult> MeasureAsync(
        string host,
        TimeSpan timeout,
        CancellationToken cancellationToken = default) {
        IPAddress address;
        try {
            var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
            if (addresses.Length == 0) return new PingProbeResult(false, null);
            address = addresses[0];
        }
        catch (Exception ex) when (ex is SocketException or ArgumentException) {
            return new PingProbeResult(false, null);
        }

        using var ping = new Ping();
        try {
            var reply = await ping.SendPingAsync(address, (int)timeout.TotalMilliseconds)
                .WaitAsync(cancellationToken);
            return new PingProbeResult(true, reply.Status == IPStatus.Success ? (int)reply.RoundtripTime : null);
        }
        catch (Exception ex) when (ex is PingException or OperationCanceledException) {
            return new PingProbeResult(true, null);
        }
    }
}
