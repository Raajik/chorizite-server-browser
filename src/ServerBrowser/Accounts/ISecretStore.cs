namespace ServerBrowser.Accounts;

public interface ISecretStore {
    void Write(string accountId, string password);
    string? Read(string accountId);
    void Delete(string accountId);
}
