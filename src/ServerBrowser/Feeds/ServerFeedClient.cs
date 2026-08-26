using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ServerBrowser.Feeds;

public sealed class ServerFeedClient : IDisposable {
    public const string CommunityServersUrl = "https://raw.githubusercontent.com/acresources/serverslist/master/Servers.xml";
    public const string PlayerCountsUrl = "http://treestats.net/player_counts-latest.json";

    private readonly HttpClient _http;
    private readonly string _cacheDirectory;

    public ServerFeedClient(string cacheDirectory) {
        _cacheDirectory = cacheDirectory;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Raajik-Chorizite-ServerBrowser/0.1");
    }

    public async Task<List<ServerListing>> RefreshAsync(CancellationToken cancellationToken = default) {
        Directory.CreateDirectory(_cacheDirectory);
        var serverXml = await GetWithCacheAsync(CommunityServersUrl, "servers.xml", required: true, cancellationToken);
        var servers = ServerFeedParser.ParseServers(serverXml);

        try {
            var countsJson = await GetWithCacheAsync(PlayerCountsUrl, "player-counts.json", required: false, cancellationToken);
            if (!string.IsNullOrWhiteSpace(countsJson)) {
                servers = ServerFeedParser.MergeCounts(servers, ServerFeedParser.ParseCounts(countsJson));
            }
        }
        catch {
            // Counts are helpful but must never prevent browsing or launching.
        }

        await ServerPingProbe.PopulateAsync(
            servers,
            TimeSpan.FromMilliseconds(750),
            maxConcurrency: 16,
            cancellationToken);

        return servers.ToList();
    }

    private async Task<string> GetWithCacheAsync(
        string url,
        string cacheName,
        bool required,
        CancellationToken cancellationToken) {
        var cachePath = Path.Combine(_cacheDirectory, cacheName);
        try {
            var content = await _http.GetStringAsync(url, cancellationToken);
            await File.WriteAllTextAsync(cachePath, content, cancellationToken);
            return content;
        }
        catch when (File.Exists(cachePath)) {
            return await File.ReadAllTextAsync(cachePath, cancellationToken);
        }
        catch when (!required) {
            return "";
        }
    }

    public void Dispose() => _http.Dispose();
}
