using System.Net;
using System.Net.Sockets;
using System.Text;
using ServerBrowser.Feeds;
using Xunit;

namespace ServerBrowser.Tests;

public class ServerLoginProbeTests {
    [Fact]
    public void PacketIsTwentyByteHeaderPlusSixtyByteLoginPayload() {
        var packet = ServerLoginProbe.BuildLoginPingPacket();

        Assert.Equal(80, packet.Length);
        Assert.Equal(60, BitConverter.ToUInt16(packet, 16)); // Size field = payload byte count
        Assert.Equal(0, BitConverter.ToUInt16(packet, 18));  // Iteration
        Assert.Equal(0u, BitConverter.ToUInt32(packet, 0));  // Sequence 0 is legal during connect
    }

    [Fact]
    public void PacketCarriesLoginRequestFlagWithTheReservedTrackerAccount() {
        var packet = ServerLoginProbe.BuildLoginPingPacket();

        Assert.Equal(ServerLoginProbe.LoginRequestFlag, BitConverter.ToUInt32(packet, 4));
        var payload = Encoding.ASCII.GetString(packet, 20, packet.Length - 20);
        Assert.Contains("1802", payload);                          // retail client version
        Assert.Contains(ServerLoginProbe.TrackerAccount, payload); // reserved no-credential account
    }

    [Fact]
    public void SixteenBitLengthStringsPadToFourByteBoundaries() {
        // "1802" (4 chars) consumes 2 + 4 + 2 pad = 8 bytes; the 28-char tracker account
        // consumes 2 + 28 + 2 pad = 32; the empty "login as" string consumes 2 + 2 pad = 4.
        var packet = ServerLoginProbe.BuildLoginPingPacket();

        Assert.Equal(4, BitConverter.ToUInt16(packet, 20));
        Assert.Equal(0, packet[26]);
        Assert.Equal(0, packet[27]);

        var accountOffset = 20 + 8 + 16; // version string, then four u32 struct fields
        Assert.Equal(28, BitConverter.ToUInt16(packet, accountOffset));
        Assert.Equal(28, ServerLoginProbe.TrackerAccount.Length);
        Assert.Equal(0, packet[accountOffset + 2 + 28]); // first pad byte
        Assert.Equal(0, packet[accountOffset + 2 + 29]); // second pad byte
    }

    [Fact]
    public void ChecksumCoversHeaderAndPayloadPerTheAceLoginRule() {
        // ACE folds the entire login payload into the optional-header checksum, so the
        // rule is Hash32(header with 0xBADD70DD) + Hash32(payload). A checksum that omits
        // the payload hash was measured live: GDLE still replies but every ACE server
        // silently drops the packet. The golden value below was verified against the
        // live community list (38 of 47 endpoints replied) before being pinned here.
        var packet = ServerLoginProbe.BuildLoginPingPacket();

        var header = (byte[])packet.Clone();
        BitConverter.GetBytes(ServerLoginProbe.ChecksumPlaceholder).CopyTo(header, 8);
        var expected = ServerLoginProbe.Hash32(header, 20)
            + ServerLoginProbe.Hash32(packet.AsSpan(20).ToArray(), packet.Length - 20);

        Assert.Equal(expected, BitConverter.ToUInt32(packet, 8));
        Assert.Equal(0xAD23481Cu, BitConverter.ToUInt32(packet, 8));
    }

    [Fact]
    public async Task ProbeAsyncReceivesLatencyWhenTheServerAnswersTheHandshake() {
        using var listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)listener.Client.LocalEndPoint!).Port;

        var serverTask = Task.Run(async () => {
            // Faithful ACE-side validation: recompute the checksum exactly the way ACE's
            // VerifyCRC does for a LoginRequest and only reply when it matches.
            var received = await listener.ReceiveAsync();
            var data = received.Buffer;
            Assert.True(data.Length >= 20);
            Assert.Equal(data.Length - 20, BitConverter.ToUInt16(data, 16));

            var header = (byte[])data.Clone();
            BitConverter.GetBytes(ServerLoginProbe.ChecksumPlaceholder).CopyTo(header, 8);
            var expected = ServerLoginProbe.Hash32(header, 20)
                + ServerLoginProbe.Hash32(data.AsSpan(20).ToArray(), data.Length - 20);
            Assert.Equal(expected, BitConverter.ToUInt32(data, 8));

            var reply = new byte[] { 0x0A, 0x00, 0x00, 0x00, 0x01 };
            await listener.SendAsync(reply, reply.Length, received.RemoteEndPoint);
        });

        var result = await ServerLoginProbe.ProbeAsync(IPAddress.Loopback, port, TimeSpan.FromSeconds(2));
        await serverTask;

        Assert.True(result.HostResolved);
        Assert.NotNull(result.LatencyMs);
        Assert.InRange(result.LatencyMs.Value, 0, 2000);
    }

    [Fact]
    public async Task ProbeAsyncReportsNullLatencyWhenTheGamePortIsSilent() {
        using var listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)listener.Client.LocalEndPoint!).Port;

        var result = await ServerLoginProbe.ProbeAsync(IPAddress.Loopback, port, TimeSpan.FromMilliseconds(300));

        Assert.True(result.HostResolved);
        Assert.Null(result.LatencyMs);
    }

    [Fact]
    public async Task MeasureAsyncFallsBackToIcmpWhenTheHandshakeGetsNoReply() {
        // A silent game port must not hide a live machine: the ICMP fallback still
        // produces a number, exactly as the pre-UDP behaviour did for this host.
        using var listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)listener.Client.LocalEndPoint!).Port;

        var result = await ServerPingProbe.MeasureAsync("127.0.0.1", port, TimeSpan.FromSeconds(1));

        Assert.True(result.HostResolved);
        Assert.NotNull(result.LatencyMs);
    }
}
