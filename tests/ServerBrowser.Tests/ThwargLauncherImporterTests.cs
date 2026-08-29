using ServerBrowser.Accounts;
using Xunit;

namespace ServerBrowser.Tests;

public class ThwargLauncherImporterTests : IDisposable {
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ThwargImportTests-{Guid.NewGuid():N}");

    private string WriteFile(string name, string content) {
        var path = Path.Combine(_directory, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void ParseAccountsReadsKeyValuesAndSkipsHeaderAndVersion() {
        var path = WriteFile("Accounts.txt",
            "# Name=abc,Password=abc,LaunchPath=abc,PreferencePath=abc,Alias=abc\r\n" +
            "Version=2\r\n" +
            "Name=testuser,Password=hunter2!\r\n" +
            "Name=other,Password=secret one,Alias=Alt Account\r\n" +
            "Name=nopassword,LaunchPath=x\r\n");

        var accounts = ThwargLauncherParser.ParseAccounts(path);

        Assert.Equal(2, accounts.Count);
        Assert.Equal("testuser", accounts[0].Username);
        Assert.Equal("hunter2!", accounts[0].Password);
        Assert.Equal("other", accounts[1].Username);
        Assert.Equal("secret one", accounts[1].Password);
        Assert.Equal("Alt Account", accounts[1].Alias);
        Assert.Null(accounts[0].Alias);
    }

    [Fact]
    public void ParseProfileDefaultsPicksFirstCharacterPerAccount() {
        var path = WriteFile("Default.txt", """
            {"AccountStates":[{"AccountName":"alice","Active":true}],
             "CharacterSettings":[
               {"AccountName":"alice","Active":true,"ChosenCharacter":"Bob","ServerName":"Eversong"},
               {"AccountName":"alice","Active":true,"ChosenCharacter":"Cat","ServerName":"Dust"},
               {"AccountName":"carol","Active":false,"ChosenCharacter":"Zed","ServerName":"Conquest"}],
             "FileVersion":"1.2.3.4"}
            """);

        var defaults = ThwargLauncherParser.ParseProfileDefaults(path);

        Assert.Equal("Eversong", defaults["alice"]);
        Assert.Equal("Conquest", defaults["carol"]);
        Assert.Equal(2, defaults.Count);
    }

    [Fact]
    public void ParseUserServerListReadsConnectStrings() {
        var path = WriteFile("UserServerList.xml", """
            <?xml version="1.0" encoding="utf-8"?>
            <ArrayOfServerItem xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
              <ServerItem>
                <id>00000000-0000-0000-0000-000000000001</id>
                <name>Synth Realm</name>
                <emu>ACE</emu>
                <connect_string>synth.example.com:2345</connect_string>
              </ServerItem>
              <ServerItem>
                <id>00000000-0000-0000-0000-000000000002</id>
                <name>Bad Port</name>
                <connect_string>bad.example.com:99999</connect_string>
              </ServerItem>
              <ServerItem>
                <id>00000000-0000-0000-0000-000000000003</id>
                <name>No Connect</name>
                <connect_string></connect_string>
              </ServerItem>
            </ArrayOfServerItem>
            """);

        var servers = ThwargLauncherParser.ParseUserServerList(path);

        var server = Assert.Single(servers);
        Assert.Equal("Synth Realm", server.Name);
        Assert.Equal("synth.example.com", server.Host);
        Assert.Equal(2345, server.Port);
    }

    [Fact]
    public void ImportSavesMetadataAndWritesPasswordsOnlyToSecretStore() {
        var secrets = new MemorySecretStore();
        var accounts = new AccountManager(Path.Combine(_directory, "data"), secrets);
        var accountsTxt = WriteFile("Accounts.txt",
            "Name=imported one,Password=imported-secret-1\nName=other,Password=other-secret");
        var importer = new ThwargLauncherImporter(accounts);

        var (imported, updated) = importer.Import(accountsTxt, null, null);

        Assert.Equal(2, imported);
        Assert.Equal(0, updated);
        var saved = accounts.GetAccounts();
        Assert.Equal(2, saved.Count);
        Assert.All(saved, account => Assert.DoesNotContain("secret", File.ReadAllText(Path.Combine(Path.Combine(_directory, "data"), "accounts.json"))));
        Assert.Equal("imported-secret-1", secrets.Read(saved[0].Id));
        Assert.Equal("imported-secret-1", accounts.GetPassword(saved[0].Id));
    }

    [Fact]
    public void ImportFillsServerDefaultFromProfileOnlyWhenUnmatchedNameResolves() {
        var secrets = new MemorySecretStore();
        var accounts = new AccountManager(Path.Combine(_directory, "data2"), secrets);
        var accountsTxt = WriteFile("Accounts2.txt", "Name=alice,Password=pw-alice\nName=bob,Password=pw2");
        var profile = WriteFile("Default.txt", """
            {"CharacterSettings":[
              {"AccountName":"alice","Active":true,"ChosenCharacter":"A","ServerName":"Eversong"},
              {"AccountName":"bob","Active":true,"ChosenCharacter":"B","ServerName":"Unknown Server"}]}
            """);
        var defaults = ThwargLauncherParser.ParseProfileDefaults(profile);
        var importer = new ThwargLauncherImporter(accounts);

        var (imported, _) = importer.Import(accountsTxt, defaults, name => name == "Eversong" ? "eversong-id" : null);

        Assert.Equal(2, imported);
        var accountsList = accounts.GetAccounts();
        var alice = accountsList.Single(a => a.Username == "alice");
        var bob = accountsList.Single(a => a.Username == "bob");
        Assert.Equal("eversong-id", alice.DefaultServerId);
        Assert.Equal("", bob.DefaultServerId);
    }

    [Fact]
    public void ImportUpdatesExistingAccountWithoutClobberingItsDefaultServer() {
        var secrets = new MemorySecretStore();
        var accounts = new AccountManager(Path.Combine(_directory, "data3"), secrets);
        var existing = accounts.Save("", "alice", "Alice Prime", "coldeve", "existing-pw");
        var accountsTxt = WriteFile("Accounts2.txt", "Name=alice,Password=imported-pw");
        var importer = new ThwargLauncherImporter(accounts);

        var (_, updated) = importer.Import(accountsTxt, null, null);

        Assert.Equal(1, updated);
        var saved = Assert.Single(accounts.GetAccounts());
        Assert.Equal(existing.Id, saved.Id);
        Assert.Equal("alice", saved.Alias); // import refreshes alias from the launcher metadata
        Assert.Equal("coldeve", saved.DefaultServerId);
        Assert.Equal("imported-pw", secrets.Read(saved.Id));
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
