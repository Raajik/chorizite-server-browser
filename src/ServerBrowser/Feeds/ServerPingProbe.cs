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

/// <summary>
/// Two-stage reachability probe:
///
/// 1. AC UDP login handshake against the game endpoint (ServerLoginProbe) — a reply is
///    real game-port reachability with genuine latency, which ICMP cannot provide for
///    the roughly half of the community list that drops echo requests.
/// 2. ICMP fallback when the handshake gets no reply — the machine may still be up even
///    when the game port is not answering, which is worth showing rather than hiding.
///
/// DNS failure still reports HostResolved = false (rendered as a red Offline) because a
/// dead name means a genuinely stale listing.
/// </summary>
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
                var result = await MeasureAsync(server.Host, server.Port, timeout, cancellationToken);
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
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken = default) {
        IPAddress? address;
        try {
            var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
            // The AC client only speaks IPv4, so prefer the A record for both stages.
            address = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                ?? addresses.FirstOrDefault();
            if (address is null) return new PingProbeResult(false, null);
        }
        catch (Exception ex) when (ex is SocketException or ArgumentException) {
            return new PingProbeResult(false, null);
        }

        var handshake = await ServerLoginProbe.ProbeAsync(
            address, port, ServerLoginProbe.DefaultTimeout, cancellationToken);
        if (handshake.LatencyMs is not null) return handshake;

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
