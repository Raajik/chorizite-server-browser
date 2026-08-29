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

        Assert.Contains("favoriteServer = isFavorite", lua);
        Assert.Matches(@"\.server\.favoriteServer \{[^}]*background-color", rml);
        // Reordering is NOT drag-and-drop (distorted ghost); order comes from
        // the Accounts tab arrows.
        Assert.DoesNotContain("onDragstart", lua);
        Assert.DoesNotContain("drag: clone", rml);
        Assert.DoesNotContain("moveFavorite(server.Id", lua);
        Assert.DoesNotContain("move-favorite", lua);
    }

    [Fact]
    public void ServerRowsHaveAnExpandablePerServerAccountPicker() {
        var lua = ReadLua();
        var rml = ReadRml();

        // Chevron implies expand/collapse; the picker is a stable child of every
        // row (hidden with .hidden), never added or removed.
        Assert.Contains("chevron-right.png", lua);
        Assert.Contains("chevron-down.png", lua);
        Assert.Contains("expandedServers[server.Id]", lua);
        Assert.Contains("class = { picker = true, hidden = state.expandedServers[server.Id] ~= true }", lua);
        Assert.Contains("toggleServerAccount(serverId, account.Id)", lua);
        Assert.Contains("'Select all'", lua);
        Assert.Contains("state.serverAccounts", lua);
        Assert.Matches(@"\.picker \{[^}]*width: 100%", rml);
        // Alternating row tints inside the picker box.
        Assert.Contains(".picker .pickRow.even { background-color:", rml);
        Assert.Contains(".picker .pickRow.odd { background-color:", rml);
        // Picks launch through the normal account-launch path.
        Assert.Contains("launchServerPicks()", lua);
        // Per-server favorite stars pin accounts to the top of that picker.
        Assert.Contains("toggleServerFavorite(serverId, account.Id)", lua);
        Assert.Contains("star-on.png", lua);
        Assert.Matches(@"\.pickStar \{[^}]*cursor: pointer", rml);
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
        // Order persists as a rank array; reorder happens through the Accounts
        // tab arrows (moveAccount), not drag & drop.
        Assert.Contains("favoriteOrder = favoriteOrder", lua);
        Assert.Contains("moveAccount(account.Id, -1)", lua);
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
        // Badges stay visible on favorites, packed two per row, instead of being hidden.
        Assert.Matches(@"\.server\.favoriteServer \.server-badges \{[^}]*flex-wrap: wrap", rml);
        Assert.DoesNotContain(".server.favoriteServer .server-badges { display: none; }", rml);
        Assert.Contains(".reorder { display: none; }", rml);
        Assert.Matches(@"\.server\.favoriteServer \{[^}]*min-height: 0", rml);
    }

    [Fact]
    public void AccountsOfferSelectAllAndManualReordering() {
        var lua = ReadLua();
        var rml = ReadRml();

        // Per-server pickers have their own Select all; reorder lives on the Accounts tab.
        Assert.Contains("'Select all'", lua);
        // Reordering is a persisted permutation, like favorites: one row per account.
        Assert.Contains("local function orderedAccounts()", lua);
        Assert.Contains("moveAccount(account.Id, -1)", lua);
        Assert.Contains("moveAccount(account.Id, 1)", lua);
        Assert.Contains("accountOrder = accountOrder", lua);
        Assert.Contains(".move-account {", rml);
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
        var rml = ReadRml();

        Assert.Contains("toggleFavorite(server.Id)", lua);
        Assert.Contains("star-on.png", lua);
        Assert.Contains("star-off.png", lua);
        Assert.Contains("alternateClients[server.Id]", lua);
        Assert.Contains("'Alternate Client'", lua);
        // Per-server multi-launch checkbox precedes the favorite star.
        Assert.Contains("toggleServerLaunch(server.Id)", lua);
        Assert.Contains("serverLaunchSelected[server.Id]", lua);
        Assert.Contains("server-heading > .checkbox.checked { background-color: #7fdb88", rml);
    }

    [Fact]
    public void AlternateClientSetupLivesInTheServerPickerWithBrowseDialog() {
        var lua = ReadLua();
        var rml = ReadRml();

        // The picker header holds Select all (left) and the Alternate client
        // checkbox (right); the path row appears only when checked.
        Assert.Contains("'Alternate Client'", lua);
        Assert.Contains("altPathRow = true, hidden = alternate.enabled ~= true", lua);
        Assert.Contains("class = 'alternatePath'", lua);
        Assert.Contains("plugin:BrowseForExecutable()", lua);
        Assert.Contains("'Browse...'", lua);
        Assert.Matches(@"\.altToggle \{[^}]*cursor: pointer", rml);
        Assert.Matches(@"\.altPathRow \{[^}]*display: flex", rml);
        // No separate tab anymore.
        Assert.DoesNotContain("activeTab = 'clients'", lua);
        Assert.DoesNotContain("AlternateClientsView", lua);
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
        Assert.Contains(".accounts-list { height: 260px", rml);
        Assert.DoesNotContain("Launch checked on selected server", lua);
        Assert.DoesNotContain("Launch checked defaults", lua);
    }

    [Fact]
    public void AccountsTabHeaderButtonsRevealSectionsAndDeleteIsDeliberate() {
        var lua = ReadLua();
        var rml = ReadRml();

        // Dead global launch buttons are gone; three header buttons remain.
        Assert.DoesNotContain("'Launch defaults'", lua);
        Assert.DoesNotContain("'Launch selected'", lua);
        Assert.DoesNotContain("launchCheckedDefaults", lua);
        Assert.DoesNotContain("launchCheckedCurrent", lua);
        Assert.DoesNotContain("selectedAccounts", lua);
        Assert.Contains("'Add Account'", lua);
        Assert.Contains("'Remove Account'", lua);
        Assert.Contains("'Backup'", lua);

        // Add form and backup section exist in the DOM always, revealed via .hidden.
        Assert.Contains("hidden = #state.accountId == 0 and state.addAccountOpen ~= true", lua);
        Assert.Contains("hidden = state.showBackup ~= true", lua);
        // Delete buttons only render in remove mode.
        Assert.Contains("hidden = state.removeMode ~= true", lua);
        Assert.Contains("'Done Removing'", lua);
        // Alternating row tints via a class, not the unsupported :nth-child.
        Assert.DoesNotContain("nth-child", rml);
        Assert.Contains(".account-row.even", rml);
        // Last-launch log on every row.
        Assert.Contains("lastLaunches", lua);
        Assert.Contains("'Never launched'", lua);
        Assert.Contains(".account-log", rml);
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
    public void LinkIconsLiveAtTheTopOfTheBadgeColumnWithDiscordFirst() {
        var lua = ReadLua();
        var rml = ReadRml();

        // The separate .server-links column is gone; both icons fold into
        // .server-badges so positions stay consistent regardless of which
        // servers have a Discord. The stable virtual-DOM invariant holds:
        // one node per row per icon, always present, toggled via .hidden.
        Assert.DoesNotContain("server-links", lua);
        Assert.DoesNotContain(".server-links", rml);
        // Discord is the first child of the badge column, ahead of emulator/type/status.
        var luaNorm = lua.Replace("\r", "");
        int discordIcon = luaNorm.IndexOf("'tag link-badge discord-icon'", StringComparison.Ordinal);
        int webIcon = luaNorm.IndexOf("'tag link-badge website-link'", StringComparison.Ordinal);
        int badges = luaNorm.IndexOf("class = 'server-badges'", StringComparison.Ordinal);
        int emulator = luaNorm.IndexOf("'tag emulator'", StringComparison.Ordinal);
        Assert.True(badges < discordIcon && discordIcon < webIcon && webIcon < emulator,
            "badge column order must be Discord, website, then emulator/type/status");
        Assert.Contains("'tag link-badge hidden'", lua);
        Assert.Contains("assets/web.png", lua);
        Assert.Contains("assets/discord.png", lua);
        Assert.Contains("'tag link-badge website-link'", lua);
        Assert.Contains("'tag link-badge discord-icon'", lua);
        Assert.Contains(".link-badge img { width: 14px; height: 14px; }", rml);
        Assert.DoesNotContain("rx:Span('Web'", lua);
    }

    [Fact]
    public void ControlsOverrideTheLauncherSkinWithFlatStyling() {
        var rml = ReadRml();

        // theme.rcss skins button at (0,0,1); .inner scoping is what outranks it.
        Assert.Matches(@"\.inner button \{[^}]*decorator: none", rml);
        // theme.rcss sets click-sound: 0x0A0003B3 on every button; scope a silent
        // override so Server Browser buttons stay quiet without touching the theme.
        Assert.Matches(@"\.inner button \{[^}]*click-sound: none", rml);
        Assert.Contains(".toolbar input, .field input {", rml);
        Assert.Contains(".inner button:hover", rml);
        Assert.Contains(".inner button[disabled]", rml);

        // Colour overrides must outrank .inner button, so they carry .inner too.
        Assert.Contains(".inner .danger", rml);
        Assert.Contains(".inner .launch", rml);
        Assert.DoesNotMatch(@"(?m)^\s*button \{", rml);
    }

    [Fact]
    public void HiddenRuleIsDeclaredLastSoItBeatsTagDisplay() {
        var rml = ReadRml();

        var hidden = rml.IndexOf(".hidden { display: none; }", StringComparison.Ordinal);
        var tag = rml.IndexOf(".tag { display: inline-block", StringComparison.Ordinal);

        Assert.True(hidden > tag, ".hidden must come after .tag or hidden badges still render");
    }

    [Fact]
    public void AccountsTabOffersThwargLauncherImport() {
        var lua = ReadLua();

        Assert.Contains("'Import from ThwargLauncher'", lua);
        Assert.Contains("plugin:ImportThwargLauncher()", lua);
        // Result runs inside pcall so a missing Thwarg install shows an error, never a crash.
        Assert.Matches(@"local function importThwarg\(\)[\s\S]*?pcall\([\s\S]*?bump\(\)", lua);
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

    [Fact]
    public void FirstRunSeedsExampleAccounts() {
        var cs = ReadCSharp("Accounts", "AccountManager.cs");
        var plugin = ReadCSharp("ServerBrowserPlugin.cs");

        // Seed happens once, only when accounts.json doesn't exist yet.
        Assert.Contains("if (!_accounts.AccountsFileExists) _accounts.SeedExamples();", plugin);
        Assert.Matches(@"SeedExamples\(\)[\s\S]*?Alias = ""Main""", cs);
        Assert.Matches(@"SeedExamples\(\)[\s\S]*?Alias = ""Mule/Buffbot""", cs);
        Assert.Matches(@"SeedExamples\(\)[\s\S]*?Username = ""Account1""", cs);
        // Examples carry no password: launching one surfaces the "no saved password" error.
        Assert.Matches(@"SeedExamples\(\)[\s\S]*?DefaultServerId = """"", cs);
    }

    private static string ReadLua() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "assets", "server-browser.lua"));

    private static string ReadCSharp(params string[] parts) =>
        File.ReadAllText(Path.GetFullPath(Path.Combine(
            new[] { AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "ServerBrowser" }
                .Concat(parts).ToArray())));

    private static string ReadRml() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "assets", "server-browser.rml"));
}