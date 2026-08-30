using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServerBrowser.Feeds;

/// <summary>
/// Speaks the Asheron's Call UDP login handshake to the actual game endpoint, the way the
/// community "server tracker" pings did: a <c>LoginRequest</c> for the reserved
/// <c>acservertracker</c> account with no credentials attached.
///
/// A server that answers is genuinely running its game port, which is far more meaningful
/// than ICMP — roughly half the community list silently drops ICMP echo. ACE answers the
/// tracker login with a ConnectResponse packet and tears the session down immediately
/// (no credentials are sent, no account row is touched, nothing is logged at info level);
/// GDLE answers any datagram. One 80-byte packet per endpoint per feed refresh.
///
/// Wire format, byte-identical to the retail client (verified against ACE's packet layer
/// and live community servers):
///
/// 20-byte header, all little-endian:
///   Sequence u32 (0 is explicitly tolerated during connect),
///   Flags u32 (LoginRequest = 0x00010000),
///   Checksum u32,
///   Id u16, Time u16, Size u16 (payload byte count), Iteration u16.
///
/// Payload (60 bytes): the login struct —
///   client version "1802" (16L string), remaining-length u32 (unused), NetAuthType u32
///   (0 = no credentials), authFlags u32 (0), timestamp u32 (0), the reserved tracker
///   account (16L string), and an empty "login as" override (16L string).
///   16L strings are a u16 byte count, the ASCII bytes, then zero padding so 2+count is a
///   multiple of 4.
///
/// Checksum rule for login packets (the part that is easy to get wrong): ACE folds the
/// ENTIRE payload into the optional-header checksum for LoginRequest packets, so
///
///   Checksum = Hash32(header with Checksum field = 0xBADD70DD, 20) + Hash32(payload, len)
///
/// Omitting the payload hash still passes GDLE (which never verifies) and silently fails
/// every ACE server — both cases were measured live before this was pinned down.
/// </summary>
public static class ServerLoginProbe {
    public const uint LoginRequestFlag = 0x00010000;

    /// <summary>Magic value ACE substitutes into the header before hashing it.</summary>
    public const uint ChecksumPlaceholder = 0xBADD70DD;

    /// <summary>Reserved no-credential login account that ACE's tracker/pong path recognizes.</summary>
    public const string TrackerAccount = "acservertracker:jj9h26hcsggc";

    /// <summary>
    /// Longest expected reply is well under half a second; the slowest successful live
    /// reply measured 177 ms. Generous without making a dead feed refresh feel slow.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(900);

    private const int HeaderSize = 20;
    private const string ClientVersion = "1802";

    private static readonly byte[] Packet = BuildLoginPingPacket();

    /// <summary>
    /// AC's proprietary 32-bit hash: the length in the high word, then every
    /// little-endian u32 word added in, then any trailing bytes shifted in from the top.
/// </summary>
    public static uint Hash32(byte[] data, int length) {
        uint checksum = (uint)length << 16;
        for (var i = 0; i + 4 <= length; i += 4) {
            checksum += BitConverter.ToUInt32(data, i);
        }
        var shift = 3;
        for (var j = length / 4 * 4; j < length; j++) {
            checksum += (uint)data[j] << (8 * shift--);
        }
        return checksum;
    }

    /// <summary>Builds the fixed 80-byte tracker login packet.</summary>
    public static byte[] BuildLoginPingPacket() {
        var payload = BuildLoginPayload();
        var packet = new byte[HeaderSize + payload.Length];

        WriteUInt32(packet, 0, 0);                          // Sequence — 0 is fine during connect
        WriteUInt32(packet, 4, LoginRequestFlag);           // Flags
        WriteUInt32(packet, 8, ChecksumPlaceholder);        // Checksum — placeholder while hashing
        WriteUInt16(packet, 12, 0);                         // Id
        WriteUInt16(packet, 14, 0);                         // Time
        WriteUInt16(packet, 16, (ushort)payload.Length);    // Size
        WriteUInt16(packet, 18, 0);                         // Iteration

        var checksum = Hash32(packet, HeaderSize);
        checksum += Hash32(payload, payload.Length);
        payload.CopyTo(packet, HeaderSize);
        WriteUInt32(packet, 8, checksum);
        return packet;
    }

    /// <summary>
    /// Sends the handshake and measures the reply. Never throws for network conditions:
    /// no reply, an ICMP port-unreachable surfaced as ConnectionReset, or a timeout all
    /// resolve to a resolved host with null latency so the caller can fall back to ICMP.
    /// </summary>
    public static async Task<PingProbeResult> ProbeAsync(
        IPAddress address,
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken = default) {
        try {
            using var client = new UdpClient();
            client.Connect(address, port);

            var stopwatch = Stopwatch.StartNew();
            await client.SendAsync(Packet, cancellationToken);

            await client.ReceiveAsync().WaitAsync(timeout, cancellationToken);
            stopwatch.Stop();
            return new PingProbeResult(true, (int)stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is SocketException or TimeoutException or OperationCanceledException) {
            return new PingProbeResult(true, null);
        }
    }

    private static byte[] BuildLoginPayload() {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        WriteString16L(writer, ClientVersion);
        writer.Write(0u);                       // remaining-data length (unused by the server)
        writer.Write(0u);                       // NetAuthType.Undef — no credentials attached
        writer.Write(0u);                       // authFlags: none
        writer.Write(0u);                       // client timestamp
        WriteString16L(writer, TrackerAccount);
        WriteString16L(writer, "");             // "login as" override (admin only)
        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteString16L(BinaryWriter writer, string value) {
        var bytes = Encoding.ASCII.GetBytes(value);
        writer.Write((ushort)bytes.Length);
        writer.Write(bytes);
        var remainder = (2 + bytes.Length) % 4;
        for (var pad = remainder == 0 ? 0 : 4 - remainder; pad > 0; pad--) {
            writer.Write((byte)0);
        }
    }

    private static void WriteUInt32(byte[] buffer, int offset, uint value) =>
        BitConverter.GetBytes(value).CopyTo(buffer, offset);

    private static void WriteUInt16(byte[] buffer, int offset, ushort value) =>
        BitConverter.GetBytes(value).CopyTo(buffer, offset);
}
