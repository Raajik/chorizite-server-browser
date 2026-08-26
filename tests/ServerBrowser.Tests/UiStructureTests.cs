using Xunit;

namespace ServerBrowser.Tests;

public class UiStructureTests {
    [Fact]
    public void FilteringKeepsEveryServerRowInTheVirtualDom() {
        var lua = ReadLua();

        Assert.Contains("rows[#rows + 1] = ServerRow(server, not matches(server))", lua);
        Assert.DoesNotContain("if matches(server) then rows[#rows + 1] = ServerRow(server) end", lua);
        Assert.Contains("filtered = isFiltered", lua);
    }

    [Fact]
    public void ToolbarUsesSearchWithoutRedundantFeedOrEmulatorButtons() {
        var lua = ReadLua();

        Assert.Contains("placeholder = 'Search name or description...'", lua);
        Assert.DoesNotContain("}, 'ACE')", lua);
        Assert.DoesNotContain("}, 'GDL')", lua);
        Assert.DoesNotContain("}, 'Refresh')", lua);
    }

    [Fact]
    public void ServerRowsRenderColorableMetadataAndDiscordSlots() {
        var lua = ReadLua();

        Assert.Contains("pve = serverType == 'pve'", lua);
        Assert.Contains("pvp = serverType == 'pvp'", lua);
        Assert.Contains("statusStable", lua);
        Assert.Contains("statusDevelopment", lua);
        Assert.Contains("statusExperimental", lua);
        Assert.Contains("discord-icon", lua);
        Assert.Contains("discord-placeholder", lua);
        Assert.Contains("plugin:OpenDiscord(server.DiscordUrl)", lua);
        Assert.Contains("e.StopPropagation()", lua);
    }

    [Fact]
    public void UnresolvableHostsReadOfflineRatherThanUnknownPing() {
        var lua = ReadLua();
        var rml = ReadRml();

        Assert.Contains("if server.HostResolved == false then return 'Offline' end", lua);
        Assert.Contains("return 'Ping: N/A'", lua);
        Assert.Contains("offline = server.HostResolved == false", lua);
        Assert.Contains(".ping.offline", rml);
    }

    [Fact]
    public void ServerRowsRenderWebsiteSlots() {
        var lua = ReadLua();
        var rml = ReadRml();

        Assert.Contains("website-link", lua);
        Assert.Contains("plugin:OpenWebsite(server.WebsiteUrl)", lua);
        Assert.Contains(".website-badge", rml);
        Assert.Contains(".website-link:hover", rml);
    }

    [Fact]
    public void WebsiteBadgeIsHiddenRatherThanRemovedWhenNoUrlExists() {
        var lua = ReadLua();
        var rml = ReadRml();

        Assert.Contains("'tag website-badge hidden'", lua);
        Assert.DoesNotContain("website-placeholder", lua);
        Assert.Contains(".hidden { display: none; }", rml);
    }

    [Fact]
    public void FavoriteRowsArePinnedAndTintedWithoutReorderingTheVirtualDom() {
        var lua = ReadLua();
        var rml = ReadRml();

        Assert.Contains("favoriteServer = state.favorites[server.Id] == true", lua);
        Assert.Matches(@"\.server\.favoriteServer \{[^}]*background-color", rml);
    }

    [Fact]
    public void OrderingNeverUsesTheUnsupportedCssOrderProperty() {
        var lua = ReadLua();
        var rml = ReadRml();

        Assert.DoesNotMatch(@"(?<![-\w])order:", rml);
        Assert.DoesNotMatch(@"(?<![-\w])order:", lua);
    }

    [Fact]
    public void FavoritesReorderThroughRankedFlexOrderRatherThanRowMoves() {
        var lua = ReadLua();

        Assert.Contains("local function pinnedFirst()", lua);
        Assert.Contains("for _, server in ipairs(pinnedFirst()) do", lua);
        Assert.Contains("for serverId, isFavorite in pairs(result.favorites) do", lua);
        Assert.Contains("moveFavorite(server.Id, -1)", lua);
        Assert.Contains("moveFavorite(server.Id, 1)", lua);
        Assert.Contains("favoriteOrder = favoriteOrder", lua);
        Assert.DoesNotContain("table.sort(state.servers", lua);
    }

    [Fact]
    public void SettingsPersistPlainTablesRatherThanReactiveProxies() {
        var lua = ReadLua();

        Assert.Contains("plainCopy(state.alternateClients)", lua);
        Assert.Contains("pcall(json.encode", lua);
        Assert.Contains("pcall(json.decode, contents)", lua);
        Assert.DoesNotContain("favorites = state.favorites,", lua);
        Assert.DoesNotContain("alternateClients = state.alternateClients\n", lua);
    }

    [Fact]
    public void FavoriteRowsCollapseToACompactCard() {
        var rml = ReadRml();

        Assert.Contains(".server.favoriteServer .server-footer { display: none; }", rml);
        Assert.Contains(".reorder { display: none; }", rml);
        Assert.Contains(".server.favoriteServer .reorder { display: flex;", rml);
        Assert.Matches(@"\.server\.favoriteServer \{[^}]*min-height: 0", rml);
    }

    [Fact]
    public void LayoutUsesServerAndAccountTabsWithFullWidthDescribedRows() {
        var lua = ReadLua();
        var rml = ReadRml();

        Assert.Contains("activeTab = 'servers'", lua);
        Assert.Contains("'Servers'", lua);
        Assert.Contains("'Accounts'", lua);
        Assert.Contains("server.Description", lua);
        Assert.Contains("'Ping: '", lua);
        Assert.DoesNotContain("class = 'details'", lua);
        Assert.Contains("text-align: center", rml);
    }

    [Fact]
    public void ServerRowsSupportFavoritesAndPerServerClientOverrides() {
        var lua = ReadLua();

        Assert.Contains("toggleFavorite(server.Id)", lua);
        Assert.Contains("star-on.png", lua);
        Assert.Contains("star-off.png", lua);
        Assert.Contains("alternateClients[server.Id]", lua);
        Assert.Contains("Use alternate client", lua);
    }

    [Fact]
    public void AccountsTabUsesCredentialBackedPluginOperations() {
        var lua = ReadLua();

        Assert.Contains("plugin:GetAccounts()", lua);
        Assert.Contains("plugin:SaveAccount", lua);
        Assert.Contains("plugin:DeleteAccount", lua);
        Assert.Contains("plugin:LaunchAccount", lua);
        Assert.Contains("plugin:ExportAccounts", lua);
        Assert.Contains("plugin:ImportAccounts", lua);
    }

    [Fact]
    public void FixedWindowLayoutUsesCompactWidthsAndHeights() {
        var lua = ReadLua();
        var rml = ReadRml();

        Assert.Contains("body { width: 780px", rml);
        Assert.Contains(".accounts-list { height: 170px", rml);
        Assert.DoesNotContain("Launch checked on selected server", lua);
        Assert.DoesNotContain("Launch checked defaults", lua);
    }

    [Fact]
    public void ServerCardKeepsEndpointInlineAndBadgesAtBottomRight() {
        var lua = ReadLua();
        var rml = ReadRml();

        Assert.Contains("class = 'title-block'", lua);
        Assert.Contains("'(' .. server.Endpoint .. ')'", lua);
        Assert.Contains("class = 'server-footer'", lua);
        Assert.Contains("class = 'server-badges'", lua);
        Assert.Contains(".title-block { display: flex", rml);
        Assert.Contains(".server-badges { display: flex", rml);
        Assert.Contains(".discord-badge", rml);
        Assert.Contains(".count { color: #7fdb88; font-size: 16px", rml);
        Assert.True(lua.IndexOf("class = 'server-badges'", StringComparison.Ordinal) >
                    lua.IndexOf("class = 'description'", StringComparison.Ordinal));
        Assert.DoesNotContain("class = 'meta'", lua);
        Assert.DoesNotContain("class = 'heading-tags'", lua);
    }

    [Fact]
    public void PrimaryLaunchButtonRedirectsUnconfiguredUsersToAccounts() {
        var lua = ReadLua();

        Assert.Contains("local function beginLaunch()", lua);
        Assert.Contains("state.activeTab = 'accounts'", lua);
        Assert.Contains("onclick = beginLaunch", lua);
        Assert.Contains("}, 'Launch')", lua);
        Assert.DoesNotContain("Launch on ", lua);
        Assert.DoesNotContain("Launch checked accounts", lua);
    }

    private static string ReadLua() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "assets", "server-browser.lua"));

    private static string ReadRml() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "assets", "server-browser.rml"));
}