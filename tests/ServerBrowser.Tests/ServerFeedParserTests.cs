using System.Linq;
using ServerBrowser.Feeds;
using Xunit;

namespace ServerBrowser.Tests;

public class ServerFeedParserTests {
    [Fact]
    public void ParseServersReadsCommunitySchemaAndBuildsEndpoint() {
        const string xml = """
            <ArrayOfServerItem>
              <ServerItem>
                <id>26d9ec3d-9fbf-4fda-95f9-8d87e005ae3a</id>
                <name>Coldeve</name>
                <description>Retail-like PvE.</description>
                <emu>ACE</emu>
                <server_host>play.coldeve.ac</server_host>
                <server_port>9000</server_port>
                <type>PvE</type>
                <status>Stable</status>
                <website_url></website_url>
                <discord_url>https://discord.gg/example</discord_url>
              </ServerItem>
            </ArrayOfServerItem>
            """;

        var servers = ServerFeedParser.ParseServers(xml);

        var server = Assert.Single(servers);
        Assert.Equal("Coldeve", server.Name);
        Assert.Equal("ACE", server.Emulator);
        Assert.Equal("play.coldeve.ac:9000", server.Endpoint);
        Assert.Equal("Stable", server.Status);
        Assert.Equal("https://discord.gg/example", server.DiscordUrl);
    }

    [Fact]
    public void DiscordInviteInWebsiteFieldBecomesTheDiscordLinkInsteadOfADuplicateBadge() {
        const string xml = """
            <ArrayOfServerItem>
              <ServerItem><id>1</id><name>Promote</name><server_host>a.test</server_host><server_port>9000</server_port>
                <website_url>https://discord.gg/promoted</website_url></ServerItem>
              <ServerItem><id>2</id><name>Drop</name><server_host>b.test</server_host><server_port>9001</server_port>
                <website_url>https://discord.gg/other</website_url><discord_url>https://discord.gg/kept</discord_url></ServerItem>
              <ServerItem><id>3</id><name>Exact</name><server_host>c.test</server_host><server_port>9002</server_port>
                <website_url>https://example.com/play</website_url><discord_url>https://example.com/play</discord_url></ServerItem>
              <ServerItem><id>4</id><name>Real</name><server_host>d.test</server_host><server_port>9003</server_port>
                <website_url>https://example.com</website_url><discord_url>https://discord.gg/kept</discord_url></ServerItem>
            </ArrayOfServerItem>
            """;

        var servers = ServerFeedParser.ParseServers(xml);

        var promoted = servers.Single(server => server.Name == "Promote");
        Assert.Equal("", promoted.WebsiteUrl);
        Assert.Equal("https://discord.gg/promoted", promoted.DiscordUrl);

        var dropped = servers.Single(server => server.Name == "Drop");
        Assert.Equal("", dropped.WebsiteUrl);
        Assert.Equal("https://discord.gg/kept", dropped.DiscordUrl);

        Assert.Equal("", servers.Single(server => server.Name == "Exact").WebsiteUrl);

        var real = servers.Single(server => server.Name == "Real");
        Assert.Equal("https://example.com", real.WebsiteUrl);
        Assert.Equal("https://discord.gg/kept", real.DiscordUrl);
    }

    [Fact]
    public void MergeCountsMatchesNamesCaseInsensitivelyAndSortsByPopulation() {
        const string xml = """
            <ArrayOfServerItem>
              <ServerItem><id>1</id><name>Quiet</name><server_host>quiet.test</server_host><server_port>9000</server_port></ServerItem>
              <ServerItem><id>2</id><name>Busy</name><server_host>busy.test</server_host><server_port>9001</server_port></ServerItem>
            </ArrayOfServerItem>
            """;
        const string json = """
            [
              { "server": "busy", "count": 42, "date": "2026-08-26 UTC", "age": "now" },
              { "server": "QUIET", "count": 3, "date": "2026-08-26 UTC", "age": "now" }
            ]
            """;

        var servers = ServerFeedParser.MergeCounts(
            ServerFeedParser.ParseServers(xml),
            ServerFeedParser.ParseCounts(json));

        Assert.Equal(["Busy", "Quiet"], servers.Select(s => s.Name));
        Assert.Equal(42, servers[0].PlayerCount);
        Assert.Equal(3, servers[1].PlayerCount);
    }

    [Fact]
    public void ParseServersSkipsEntriesWithoutHostOrValidPort() {
        const string xml = """
            <ArrayOfServerItem>
              <ServerItem><id>1</id><name>No Host</name><server_host></server_host><server_port>9000</server_port></ServerItem>
              <ServerItem><id>2</id><name>Bad Port</name><server_host>bad.test</server_host><server_port>nope</server_port></ServerItem>
              <ServerItem><id>3</id><name>Good</name><server_host>good.test</server_host><server_port>9000</server_port></ServerItem>
            </ArrayOfServerItem>
            """;

        var server = Assert.Single(ServerFeedParser.ParseServers(xml));
        Assert.Equal("Good", server.Name);
    }

    [Fact]
    public void ParseServersNormalizesSparseButLaunchableEntries() {
        const string xml = """
            <ArrayOfServerItem>
              <ServerItem>
                <id></id>
                <name></name>
                <description></description>
                <emu></emu>
                <server_host>sparse.example</server_host>
                <server_port>9010</server_port>
                <type></type>
                <status></status>
              </ServerItem>
            </ArrayOfServerItem>
            """;

        var server = Assert.Single(ServerFeedParser.ParseServers(xml));

        Assert.Equal("sparse.example:9010", server.Id);
        Assert.Equal("Unnamed server", server.Name);
        Assert.Contains("acresources/serverslist", server.Description);
        Assert.Equal("Unknown", server.Emulator);
        Assert.Equal("Unspecified", server.Type);
        Assert.Equal("Unspecified", server.Status);
        Assert.Equal("sparse.example:9010", server.Endpoint);
    }
}
