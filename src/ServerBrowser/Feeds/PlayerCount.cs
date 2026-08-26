namespace ServerBrowser.Feeds;

public sealed class PlayerCount {
    public string Server { get; init; } = "";
    public int Count { get; init; }
    public string Date { get; init; } = "";
    public string Age { get; init; } = "";
}
