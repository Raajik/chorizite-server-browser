using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace ServerBrowser.Accounts;

public sealed record ThwargAccount(string Username, string Password, string? Alias);
public sealed record ThwargServer(string Name, string Host, int Port);

/// <summary>
/// Pure parser for ThwargLauncher's on-disk files. Values are never logged;
/// callers decide what to persist. Supports the observed formats:
/// Accounts.txt (comma-separated key=value lines with a "# Name=..." header
/// and a "Version=" row), Profiles/*.txt (JSON with CharacterSettings), and
/// Servers/UserServerList.xml (ServerItem elements with connect_string).
/// </summary>
public static class ThwargLauncherParser {
    public static List<ThwargAccount> ParseAccounts(string accountsTxtPath) {
        var accounts = new List<ThwargAccount>();
        foreach (var rawLine in File.ReadAllLines(accountsTxtPath)) {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("Version=")) continue;
            string? name = null, password = null, alias = null;
            foreach (var segment in line.Split(',')) {
                var equals = segment.IndexOf('=');
                if (equals <= 0) continue;
                var key = segment[..equals].Trim();
                var value = segment[(equals + 1)..];
                switch (key) {
                    case "Name": name = value; break;
                    case "Password": password = value; break;
                    case "Alias": alias = string.IsNullOrWhiteSpace(value) ? null : value; break;
                }
            }
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrEmpty(password)) {
                accounts.Add(new ThwargAccount(name, password, alias));
            }
        }
        return accounts;
    }

    /// <summary>Maps account name → the server of its first listed character, from a profile JSON.</summary>
    public static Dictionary<string, string> ParseProfileDefaults(string profilePath) {
        var defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(profilePath)) return defaults;
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(profilePath));
        if (!doc.RootElement.TryGetProperty("CharacterSettings", out var characters)) return defaults;
        foreach (var character in characters.EnumerateArray()) {
            var accountName = character.TryGetProperty("AccountName", out var a) ? a.GetString() : null;
            var serverName = character.TryGetProperty("ServerName", out var s) ? s.GetString() : null;
            if (string.IsNullOrWhiteSpace(accountName) || string.IsNullOrWhiteSpace(serverName)) continue;
            // First entry per account wins: ThwargLauncher lists the chosen character first.
            if (!defaults.ContainsKey(accountName)) defaults[accountName] = serverName;
        }
        return defaults;
    }

    public static List<ThwargServer> ParseUserServerList(string serverListPath) {
        var servers = new List<ThwargServer>();
        var document = new XmlDocument();
        document.Load(serverListPath);
        var items = document.DocumentElement?.SelectNodes("ServerItem");
        if (items is null) return servers;
        foreach (var item in items.Cast<XmlNode>()) {
            var connect = (item["connect_string"]?.InnerText ?? "").Trim();
            var colon = connect.IndexOf(':');
            if (colon <= 0) continue;
            var host = connect[..colon];
            if (!int.TryParse(connect[(colon + 1)..], out var port) || port is < 1 or > 65535) continue;
            servers.Add(new ThwargServer(item["name"]?.InnerText ?? "", host, port));
        }
        return servers;
    }
}

/// <summary>
/// Imports parsed ThwargLauncher data into an AccountManager. Passwords are
/// written only through ISecretStore; metadata goes through AccountManager.
/// Collision behavior: an existing saved account with the same normalized
/// username is updated in place (its existing default server is preserved);
/// an import never overwrites a differently-named saved account.
/// </summary>
public sealed class ThwargLauncherImporter {
    private readonly AccountManager _accounts;

    public ThwargLauncherImporter(AccountManager accounts) => _accounts = accounts;

    /// <param name="resolveServerId">Maps a Thwarg server name to this plugin's server ID, or null when unmatched.</param>
    public (int Imported, int Updated) Import(
        string accountsTxtPath,
        IReadOnlyDictionary<string, string>? profileDefaults,
        Func<string, string?>? resolveServerId) {
        var imported = 0;
        var updated = 0;
        foreach (var account in ThwargLauncherParser.ParseAccounts(accountsTxtPath)) {
            var existing = _accounts.GetAccounts().Find(saved =>
                saved.Username.Equals(account.Username.Trim(), StringComparison.OrdinalIgnoreCase));
            string? serverId = null;
            if (profileDefaults is not null &&
                profileDefaults.TryGetValue(account.Username, out var serverName) &&
                resolveServerId is not null) {
                serverId = resolveServerId(serverName);
            }
            // Preserve an existing default rather than silently changing it.
            var defaultServerId =
                !string.IsNullOrEmpty(existing?.DefaultServerId) ? existing!.DefaultServerId
                : serverId ?? "";
            _accounts.Save(existing?.Id ?? "", account.Username, account.Alias ?? account.Username, defaultServerId, account.Password);
            if (existing is null) imported++; else updated++;
        }
        return (imported, updated);
    }
}
