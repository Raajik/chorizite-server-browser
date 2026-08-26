namespace ServerBrowser.Feeds;

public sealed class ServerListing {
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Emulator { get; init; } = "";
    public string Host { get; init; } = "";
    public int Port { get; init; }
    public string Type { get; init; } = "";
    public string Status { get; init; } = "";
    public string WebsiteUrl { get; init; } = "";
    public string DiscordUrl { get; init; } = "";
    public int? PlayerCount { get; set; }
    public int? PingMs { get; set; }
    public string CountAge { get; set; } = "";
    public string Endpoint => $"{Host}:{Port}";
}
