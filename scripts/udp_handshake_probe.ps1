# Empirical audit of the AC UDP login-handshake probe against the live community list.
#
# Sends the same 80-byte tracker login packet the plugin's ServerLoginProbe sends and
# reports, per unique host:port in the cached feed, whether the game endpoint answered,
# how fast, and what ICMP says about the same host for comparison.
#
# Usage:  powershell -NoProfile -ExecutionPolicy Bypass -File scripts/udp_handshake_probe.ps1
# Needs:  C:\Games\Chorizite\data\ServerBrowser\cache\servers.xml (the plugin's cache);
#         override $cacheXml below to audit a different list.

$ErrorActionPreference = 'Continue'

# --- packet construction (mirrors src/ServerBrowser/Feeds/ServerLoginProbe.cs) ---------------

function Get-Hash32([byte[]]$data, [int]$length) {
    # AC's proprietary hash: length in the high word, then little-endian u32 words.
    $checksum = [uint32]([uint64]$length -shl 16)
    for ($i = 0; $i + 4 -le $length; $i += 4) {
        $word = [BitConverter]::ToUInt32($data, $i)
        $checksum = [uint32](($checksum + $word) % 4294967296)
    }
    return $checksum
}

function Write-Str16L([System.IO.BinaryWriter]$w, [string]$s) {
    $bytes = [System.Text.Encoding]::ASCII.GetBytes($s)
    $w.Write([uint16]$bytes.Length)
    $w.Write($bytes)
    $pad = (4 * [math]::Ceiling((2 + $bytes.Length) / 4)) - (2 + $bytes.Length)
    for ($i = 0; $i -lt $pad; $i++) { $w.Write([byte]0) }
}

function New-LoginPingPacket {
    # Payload: client version "1802", unused length u32, NetAuthType 0 (no credentials),
    # authFlags 0, timestamp 0, the reserved tracker account, empty "login as" string.
    $payload = New-Object System.IO.MemoryStream
    $pw = New-Object System.IO.BinaryWriter($payload)
    Write-Str16L $pw "1802"
    $pw.Write([uint32]0); $pw.Write([uint32]0); $pw.Write([uint32]0); $pw.Write([uint32]0)
    Write-Str16L $pw "acservertracker:jj9h26hcsggc"
    Write-Str16L $pw ""
    $pw.Flush()
    $payloadBytes = $payload.ToArray()

    $header = New-Object System.IO.MemoryStream
    $hw = New-Object System.IO.BinaryWriter($header)
    $hw.Write([uint32]0)                       # Sequence
    $hw.Write([uint32]0x00010000)              # Flags = LoginRequest
    $hw.Write([Convert]::ToUInt32("BADD70DD", 16))  # Checksum placeholder
    $hw.Write([uint16]0)                       # Id
    $hw.Write([uint16]0)                       # Time
    $hw.Write([uint16]$payloadBytes.Length)    # Size
    $hw.Write([uint16]0)                       # Iteration
    $hw.Flush()

    # ACE folds the entire login payload into the optional-header checksum for
    # LoginRequest packets: checksum = Hash32(header) + Hash32(payload). Omitting the
    # payload hash passes GDLE (which never verifies) and silently fails every ACE server.
    $headerCsum = Get-Hash32 $header.ToArray() 20
    $payloadCsum = Get-Hash32 $payloadBytes $payloadBytes.Length
    $checksum = [uint32](($headerCsum + $payloadCsum) % 4294967296)

    $packet = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter($packet)
    $w.Write([uint32]0)
    $w.Write([uint32]0x00010000)
    $w.Write([uint32]$checksum)
    $w.Write([uint16]0); $w.Write([uint16]0)
    $w.Write([uint16]$payloadBytes.Length)
    $w.Write([uint16]0)
    $w.Write($payloadBytes)
    $w.Flush()
    return ,@($packet.ToArray(), $checksum, $payloadBytes.Length)
}

# --- audit -----------------------------------------------------------------------------------

$cacheXml = "C:\Games\Chorizite\data\ServerBrowser\cache\servers.xml"
$packetInfo = New-LoginPingPacket
$packet = $packetInfo[0]
"packet: $($packet.Length) bytes (header 20 + payload $($packetInfo[2])), checksum 0x{0:X8}" -f $packetInfo[1]

$xml = [xml](Get-Content $cacheXml -Raw)
$servers = @($xml.ArrayOfServerItem.ServerItem)
$emuByHost = @{}
foreach ($s in $servers) {
    $key = "$($s.server_host):$($s.server_port)".ToLower()
    if (-not $emuByHost.ContainsKey($key)) { $emuByHost[$key] = New-Object System.Collections.Generic.HashSet[string] }
    [void]$emuByHost[$key].Add($s.emu)
}
$targets = $emuByHost.Keys | Sort-Object
"feed entries: $($servers.Count), unique endpoints: $($targets.Count)"

$results = New-Object System.Collections.Generic.List[string]
foreach ($key in $targets) {
    $parts = $key -split ':'
    $hostName = $parts[0]
    $port = [int]$parts[1]
    $emu = ($emuByHost[$key] | Sort-Object) -join ','

    $ip = $null
    $dnsState = "ok"
    try {
        $addrs = [System.Net.Dns]::GetHostAddresses($hostName)
        $ip = $addrs | Where-Object { $_.AddressFamily -eq 'InterNetwork' } | Select-Object -First 1
        if ($null -eq $ip) { $ip = $addrs | Select-Object -First 1 }
    } catch { $dnsState = "fail" }

    $udpState = "silent"; $udpMs = ""
    if ($null -ne $ip) {
        $client = New-Object System.Net.Sockets.UdpClient
        try {
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            [void]$client.Connect($ip, $port)
            [void]$client.Send($packet, $packet.Length)
            try {
                $remote = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, 0)
                [void]$client.Receive([ref]$remote)
                $sw.Stop()
                $udpMs = $sw.ElapsedMilliseconds
                $udpState = "reply"
            } catch [System.Net.Sockets.SocketException] {
                $udpState = switch ($_.Exception.SocketErrorCode) {
                    'ConnectionReset' { "port-closed" }
                    default           { "silent" }
                }
            }
        } catch { $udpState = "send-fail" } finally { $client.Close() }
    }

    $icmpMs = ""; $icmpState = "fail"
    if ($null -ne $ip) {
        $ping = New-Object System.Net.NetworkInformation.Ping
        try {
            $reply = $ping.Send($ip, 1000)
            if ($reply.Status -eq 'Success') { $icmpState = "ok"; $icmpMs = $reply.RoundtripTime }
            else { $icmpState = $reply.Status }
        } catch { $icmpState = "error" }
    }

    $results.Add(("{0,-32} {1,-6} {2,-5} {3,-11} {4,-6} {5,-28} {6}" -f $key, $emu, $dnsState, $udpState, $udpMs, $icmpState, $icmpMs))
}

""
"host:port                        emu    dns   udp         reply-ms  icmp                        icmp-ms"
$results | ForEach-Object { $_ }
""
"replies: {0}/{1}" -f (($results | Where-Object { $_ -match '\sreply\s' }).Count), $results.Count
