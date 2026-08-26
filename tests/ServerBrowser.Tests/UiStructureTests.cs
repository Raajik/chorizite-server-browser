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
        Assert.Contains("clearSearch = true, hidden = #(state.query or '') == 0", lua);
        Assert.Contains("onclick = function() state.query = ''; bump() end", lua);
        Assert.Contains(".clearSearch", ReadRml());
        // A themed button overflows the toolbar, so this control stays a span.
        Assert.DoesNotContain("rx:Button({\n        class = { clearSearch", lua);
        Assert.Contains("rx:Span('X'", lua);
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
        Assert.Contains(".link-badge", rml);
        Assert.Contains(".website-link:hover", rml);
    }

    [Fact]
    public void WebsiteBadgeIsHiddenRatherThanRemovedWhenNoUrlExists() {
        var lua = ReadLua();
        var rml = ReadRml();

        Assert.Contains("'tag link-badge hidden'", lua);
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

        Assert.Contains(".server.favoriteServer .description { display: none; }", rml);
        Assert.Contains(".server.favoriteServer .server-badges { display: none; }", rml);
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
    public void ServerCardSplitsIntoTextBadgeColumnAndStatsCube() {
        var lua = ReadLua();
        var rml = ReadRml();

        Assert.Contains("class = 'title-block'", lua);
        Assert.Contains("'(' .. server.Endpoint .. ')'", lua);
        Assert.Contains("class = 'server-main'", lua);
        Assert.Contains("class = 'server-badges'", lua);
        Assert.Contains("class = 'stats-cube'", lua);
        Assert.Contains(".server { display: flex", rml);
        Assert.Contains(".server-main { flex: 1", rml);
        Assert.Contains(".title-block { display: flex", rml);
        Assert.DoesNotContain("class = 'server-footer'", lua);
        Assert.DoesNotContain(".server-footer", rml);
        Assert.DoesNotContain("class = 'stats'", lua);

        Assert.True(lua.IndexOf("class = 'stats-cube'", StringComparison.Ordinal) >
                    lua.IndexOf("class = 'server-badges'", StringComparison.Ordinal));
    }

    [Fact]
    public void BadgesStackVerticallyAndStatsCubeCarriesPingBand() {
        var rml = ReadRml();

        Assert.Contains(".server-badges { display: flex; flex-direction: column;", rml);
        Assert.Matches(@"\.server-badges \.tag \{[^}]*font-size: 10px", rml);
        Assert.Matches(@"\.stats-cube \{[^}]*border-radius", rml);
        Assert.Matches(@"\.stats-cube \.ping \{[^}]*border-top: 1px", rml);
        Assert.Contains(".stats-cube .count { display: block", rml);
    }

    [Fact]
    public void WebAndDiscordRenderAsMatchingIconsBesideTheBadgeColumn() {
        var lua = ReadLua();
        var rml = ReadRml();

        Assert.Contains("class = 'server-links'", lua);
        Assert.Contains(".server-links { display: flex; flex-direction: column; justify-content: center;", rml);

        Assert.True(lua.IndexOf("class = 'server-links'", StringComparison.Ordinal) >
                    lua.IndexOf("class = 'description'", StringComparison.Ordinal),
            "links belong outside the text column, between it and the badges");
        Assert.True(lua.IndexOf("class = 'server-badges'", StringComparison.Ordinal) >
                    lua.IndexOf("class = 'server-links'", StringComparison.Ordinal));
        Assert.Contains("assets/web.png", lua);
        Assert.Contains("assets/discord.png", lua);
        Assert.Contains("'tag link-badge website-link'", lua);
        Assert.Contains("'tag link-badge discord-icon'", lua);
        Assert.Contains(".link-badge img { width: 14px; height: 14px; }", rml);
        Assert.DoesNotContain("rx:Span('Web'", lua);
    }

    [Fact]
    public void HiddenRuleIsDeclaredLastSoItBeatsTagDisplay() {
        var rml = ReadRml();

        var hidden = rml.IndexOf(".hidden { display: none; }", StringComparison.Ordinal);
        var tag = rml.IndexOf(".tag { display: inline-block", StringComparison.Ordinal);

        Assert.True(hidden > tag, ".hidden must come after .tag or hidden badges still render");
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