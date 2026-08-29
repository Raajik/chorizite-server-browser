using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        ISecretStore secrets;
        if (WindowsCredentialStore.IsAvailable()) {
            secrets = new WindowsCredentialStore("Raajik.Chorizite.ServerBrowser");
        }
        else {
            secrets = new UnavailableSecretStore();
            _log.LogWarning("{Reason} Browsing and launching without saved accounts still work.", UnavailableSecretStore.Reason);
        }

        _accounts = new AccountManager(DataDirectory, secrets);
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

    public string ImportThwargLauncher() {
        var accounts = RequireAccounts();
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var thwargHome = Path.Combine(roaming, "ThwargLauncher");
        var accountsTxt = Path.Combine(thwargHome, "Accounts.txt");
        if (!File.Exists(accountsTxt)) {
            throw new FileNotFoundException("ThwargLauncher Accounts.txt was not found in %APPDATA%\\ThwargLauncher");
        }

        var profile = Directory.GetFiles(Path.Combine(thwargHome, "Profiles"), "*.txt")
            .OrderBy(File.GetLastWriteTimeUtc).LastOrDefault();
        var defaults = profile is null ? null : ThwargLauncherParser.ParseProfileDefaults(profile);
        var importer = new ThwargLauncherImporter(accounts);

        // Match Thwarg server names against the cached feed by endpoint, then by name.
        var servers = ThwargLauncherParser.ParseUserServerList(Path.Combine(thwargHome, "Servers", "UserServerList.xml"));
        var cachedServers = _feedClient?.RefreshAsync().GetAwaiter().GetResult() ?? [];
        string? ResolveServerId(string serverName) {
            foreach (var thwarg in servers) {
                if (!thwarg.Name.Equals(serverName, StringComparison.OrdinalIgnoreCase)) continue;
                var endpoint = $"{thwarg.Host}:{thwarg.Port}";
                var match = cachedServers.Find(s => s.Endpoint.Equals(endpoint, StringComparison.OrdinalIgnoreCase));
                return match?.Id;
            }
            // Fall back to a direct feed-name match.
            var byName = cachedServers.Find(s => s.Name.Equals(serverName, StringComparison.OrdinalIgnoreCase));
            return byName?.Id;
        }

        var (imported, updated) = importer.Import(accountsTxt, defaults, ResolveServerId);
        _log.LogInformation("ThwargLauncher import: {Imported} new, {Updated} updated", imported, updated);
        return $"Imported {imported} and updated {updated} accounts from ThwargLauncher";
    }

    public void OpenDiscord(string url) {
        if (!DiscordLink.TryOpen(url, out var error)) {
            _log.LogWarning("Unable to open Discord link {DiscordUrl}: {Error}", url, error);
        }
    }

    public void OpenWebsite(string url) {
        if (!WebsiteLink.TryOpen(url, out var error)) {
            _log.LogWarning("Unable to open website link {WebsiteUrl}: {Error}", url, error);
        }
    }

    /// <summary>Opens a native file picker and returns the chosen .exe path, or null when cancelled or unavailable.</summary>
    public string? BrowseForExecutable() {
        FileDialog.Log = m => _log.LogInformation("{Message}", m);
        try {
            var path = FileDialog.PickExecutable();
            _log.LogInformation("BrowseForExecutable returned {Result}", path ?? "(cancelled/unavailable)");
            return path;
        }
        catch (Exception ex) {
            _log.LogError(ex, "BrowseForExecutable failed");
            throw;
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
