using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace ServerBrowser.Feeds;

public static class ServerFeedParser {
    public static IReadOnlyList<ServerListing> ParseServers(string xml) {
        var document = XDocument.Parse(xml, LoadOptions.None);
        return document.Descendants("ServerItem")
            .Select(ParseServer)
            .Where(server => server is not null)
            .Cast<ServerListing>()
            .OrderBy(server => server.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<PlayerCount> ParseCounts(string json) {
        return JsonSerializer.Deserialize<List<PlayerCount>>(json, new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true
        }) ?? [];
    }

    public static IReadOnlyList<ServerListing> MergeCounts(
        IReadOnlyList<ServerListing> servers,
        IReadOnlyList<PlayerCount> counts) {
        var byName = counts
            .GroupBy(count => count.Server, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var server in servers) {
            if (!byName.TryGetValue(server.Name, out var count)) continue;
            server.PlayerCount = count.Count;
            server.CountAge = count.Age;
        }

        return servers
            .OrderByDescending(server => server.PlayerCount ?? -1)
            .ThenBy(server => server.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ServerListing? ParseServer(XElement element) {
        var host = Value(element, "server_host").Trim();
        if (host.Length == 0 || !int.TryParse(Value(element, "server_port"), out var port) || port is < 1 or > 65535) {
            return null;
        }

        return new ServerListing {
            Id = Value(element, "id"),
            Name = Value(element, "name"),
            Description = Value(element, "description"),
            Emulator = Value(element, "emu"),
            Host = host,
            Port = port,
            Type = Value(element, "type"),
            Status = Value(element, "status"),
            WebsiteUrl = Value(element, "website_url"),
            DiscordUrl = Value(element, "discord_url")
        };
    }

    private static string Value(XElement element, string name) => element.Element(name)?.Value ?? "";
}
