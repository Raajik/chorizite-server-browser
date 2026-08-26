using System;

namespace ServerBrowser.Accounts;

/// <summary>
/// Used when the host cannot reach Windows Credential Manager. Browsing and anonymous
/// launching keep working; only password operations report why they are unavailable.
/// </summary>
public sealed class UnavailableSecretStore : ISecretStore {
    public const string Reason =
        "Saved account passwords need Windows Credential Manager, which this host does not provide.";

    public void Write(string accountId, string password) => throw new PlatformNotSupportedException(Reason);

    public string? Read(string accountId) => throw new PlatformNotSupportedException(Reason);

    // Nothing was ever stored, so removing an account's metadata should still succeed.
    public void Delete(string accountId) { }
}
