using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace ServerBrowser.Feeds;

public static class ServerFeedParser {
    private const string MissingDescription =
        "No description has been provided. Server owners can update this listing at github.com/acresources/serverslist.";

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

        var endpoint = $"{host}:{port}";
        var (websiteUrl, discordUrl) = ResolveLinks(Value(element, "website_url"), Value(element, "discord_url"));

        return new ServerListing {
            Id = OrDefault(Value(element, "id"), endpoint),
            Name = OrDefault(Value(element, "name"), "Unnamed server"),
            Description = OrDefault(Value(element, "description"), MissingDescription),
            Emulator = OrDefault(Value(element, "emu"), "Unknown"),
            Host = host,
            Port = port,
            Type = OrDefault(Value(element, "type"), "Unspecified"),
            Status = OrDefault(Value(element, "status"), "Unspecified"),
            WebsiteUrl = websiteUrl,
            DiscordUrl = discordUrl
        };
    }

    private static (string Website, string Discord) ResolveLinks(string website, string discord) {
        website = website.Trim();
        discord = discord.Trim();

        if (DiscordLink.IsSupported(website)) {
            return ("", discord.Length == 0 ? website : discord);
        }

        return string.Equals(website, discord, StringComparison.OrdinalIgnoreCase)
            ? ("", discord)
            : (website, discord);
    }

    private static string Value(XElement element, string name) => element.Element(name)?.Value ?? "";

    private static string OrDefault(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
