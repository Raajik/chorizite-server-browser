using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Chorizite.Core.Backend.Launcher;
using Chorizite.Core.Plugins;
using Chorizite.Core.Plugins.AssemblyLoader;
using Microsoft.Extensions.Logging;
using RmlUi;
using RmlUi.Lib;
using ServerBrowser.Accounts;
using ServerBrowser.Feeds;

namespace ServerBrowser;

public sealed class ServerBrowserPlugin : IPluginCore {
    private readonly ILauncherBackend _launcher;
    private readonly RmlUiPlugin _rmlUi;
    private readonly ILogger _log;
    private AccountManager? _accounts;
    private ServerFeedClient? _feedClient;
    private Panel? _panel;

    public ServerBrowserPlugin(
        AssemblyPluginManifest manifest,
        ILauncherBackend launcher,
        RmlUiPlugin rmlUi,
        ILogger<ServerBrowserPlugin> log) : base(manifest) {
        _launcher = launcher;
        _rmlUi = rmlUi;
        _log = log;
    }

    protected override void Initialize() {
        Directory.CreateDirectory(DataDirectory);
        _accounts = new AccountManager(
            DataDirectory,
            new WindowsCredentialStore("Raajik.Chorizite.ServerBrowser"));
        _feedClient = new ServerFeedClient(Path.Combine(DataDirectory, "cache"));
        _panel = _rmlUi.CreatePanel("Server Browser", Path.Combine(AssemblyDirectory, "assets", "server-browser.rml"));
        if (_panel is null) {
            _log.LogError("Unable to create the Server Browser panel");
            return;
        }

        _panel.ShowInBar = true;
        _panel.Show();
    }

    public Task<List<ServerListing>> RefreshServers() {
        if (_feedClient is null) throw new InvalidOperationException("Server browser is not initialized");
        return _feedClient.RefreshAsync();
    }

    public string GetDefaultClientPath() => _launcher.GetDefaultClientPath();

    public List<SavedAccount> GetAccounts() => RequireAccounts().GetAccounts();

    public SavedAccount SaveAccount(
        string id,
        string username,
        string alias,
        string defaultServerId,
        string password) =>
        RequireAccounts().Save(id, username, alias, defaultServerId, password);

    public void DeleteAccount(string id) => RequireAccounts().Delete(id);

    public void LaunchAccount(string id, string clientPath, string endpoint) {
        var account = RequireAccounts().GetAccounts().Find(item => item.Id == id)
            ?? throw new ArgumentException("Saved account was not found", nameof(id));
        _launcher.LaunchClient(clientPath, endpoint, account.Username, RequireAccounts().GetPassword(id));
    }

    public void ExportAccounts(string path, string masterPassword) =>
        RequireAccounts().ExportBackup(path, masterPassword);

    public List<SavedAccount> ImportAccounts(string path, string masterPassword) {
        RequireAccounts().ImportBackup(path, masterPassword);
        return RequireAccounts().GetAccounts();
    }

    public void OpenDiscord(string url) {
        if (!DiscordLink.TryOpen(url, out var error)) {
            _log.LogWarning("Unable to open Discord link {DiscordUrl}: {Error}", url, error);
        }
    }

    public void Launch(string clientPath, string endpoint, string username, string password) {
        if (string.IsNullOrWhiteSpace(endpoint)) throw new ArgumentException("Select a server first", nameof(endpoint));
        _launcher.LaunchClient(clientPath, endpoint, username, password);
    }

    protected override void Dispose() {
        _panel?.Dispose();
        _panel = null;
        _feedClient?.Dispose();
        _feedClient = null;
        _accounts = null;
    }

    private AccountManager RequireAccounts() =>
        _accounts ?? throw new InvalidOperationException("Server browser is not initialized");
}
