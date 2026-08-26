namespace ServerBrowser.Accounts;

public sealed class SavedAccount {
    public string Id { get; init; } = "";
    public string Username { get; init; } = "";
    public string Alias { get; init; } = "";
    public string DefaultServerId { get; init; } = "";
}
