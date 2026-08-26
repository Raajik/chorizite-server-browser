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
using ServerBrowser.Feeds;

namespace ServerBrowser;

public sealed class ServerBrowserPlugin : IPluginCore {
    private readonly ILauncherBackend _launcher;
    private readonly RmlUiPlugin _rmlUi;
    private readonly ILogger _log;
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

    public void Launch(string clientPath, string endpoint, string username, string password) {
        if (string.IsNullOrWhiteSpace(endpoint)) throw new ArgumentException("Select a server first", nameof(endpoint));
        _launcher.LaunchClient(clientPath, endpoint, username, password);
    }

    protected override void Dispose() {
        _panel?.Dispose();
        _panel = null;
        _feedClient?.Dispose();
        _feedClient = null;
    }
}
