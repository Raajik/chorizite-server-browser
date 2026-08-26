using ServerBrowser.Accounts;
using Xunit;

namespace ServerBrowser.Tests;

public class AccountManagerTests : IDisposable {
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ServerBrowserTests-{Guid.NewGuid():N}");

    [Fact]
    public void SavePersistsAccountMetadataWithoutWritingPasswordToDisk() {
        var secrets = new MemorySecretStore();
        var manager = new AccountManager(_directory, secrets);

        var account = manager.Save(
            id: "",
            username: "raajik",
            alias: "Main",
            defaultServerId: "coldeve",
            password: "correct horse battery staple");

        var reloaded = new AccountManager(_directory, secrets).GetAccounts();
        var saved = Assert.Single(reloaded);
        Assert.Equal(account.Id, saved.Id);
        Assert.Equal("raajik", saved.Username);
        Assert.Equal("Main", saved.Alias);
        Assert.Equal("coldeve", saved.DefaultServerId);
        Assert.Equal("correct horse battery staple", secrets.Read(account.Id));
        Assert.DoesNotContain("correct horse battery staple", File.ReadAllText(Path.Combine(_directory, "accounts.json")));
    }

    [WindowsOnlyFact]
    public void WindowsCredentialStoreRoundTripsSecret() {
        var accountId = $"test-{Guid.NewGuid():N}";
        var store = new WindowsCredentialStore("Raajik.Chorizite.ServerBrowser.Tests");
        try {
            store.Write(accountId, "temporary test secret");
            Assert.Equal("temporary test secret", store.Read(accountId));
        }
        finally {
            store.Delete(accountId);
        }

        Assert.Null(store.Read(accountId));
    }

    [Fact]
    public void EncryptedBackupRestoresMetadataAndSecretsWithMasterPassword() {
        var sourceSecrets = new MemorySecretStore();
        var source = new AccountManager(Path.Combine(_directory, "source"), sourceSecrets);
        source.Save("account-1", "raajik", "Main", "coldeve", "backup secret");
        var backupPath = Path.Combine(_directory, "accounts.csb-backup");

        source.ExportBackup(backupPath, "long backup passphrase");

        Assert.DoesNotContain("backup secret", File.ReadAllText(backupPath));
        var restoredSecrets = new MemorySecretStore();
        var restored = new AccountManager(Path.Combine(_directory, "restored"), restoredSecrets);
        restored.ImportBackup(backupPath, "long backup passphrase");
        var account = Assert.Single(restored.GetAccounts());
        Assert.Equal("account-1", account.Id);
        Assert.Equal("backup secret", restoredSecrets.Read("account-1"));
    }

    public void Dispose() {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private sealed class MemorySecretStore : ISecretStore {
        private readonly Dictionary<string, string> _values = [];

        public void Write(string accountId, string password) => _values[accountId] = password;
        public string? Read(string accountId) => _values.GetValueOrDefault(accountId);
        public void Delete(string accountId) => _values.Remove(accountId);
    }
}
