# Community Server Browser — Handoff

## Project boundary

This is a **standalone Chorizite launcher plugin**. It is intentionally separate from Juggernaut.

- Server Browser workspace: `A:\ai\projects\chorizite-server-browser`
- Juggernaut workspace: `A:\ai\projects\ac-juggernaut`
- Installed plugin: `C:\Games\Chorizite\plugins\ServerBrowser`
- Runtime data/cache: `C:\Games\Chorizite\data\ServerBrowser`

**Do not add server-list, launcher, account, or server-discovery code to Juggernaut.** Juggernaut is a Chorizite `Client`-environment plugin for in-game combat/gameplay automation. Server Browser is a Chorizite `Launcher`-environment plugin for choosing and launching servers.

## Current version and status

Current plugin version: **0.10.11**

Verified on Windows 11 with:

- Chorizite core/launcher 0.0.15
- Lua plugin 0.0.13
- RmlUi plugin 0.0.11
- Launcher plugin 0.0.9
- .NET 8 x86 runtime

Last verification:

- 73/73 tests passing
- build succeeds with 0 warnings and 0 errors
- Chorizite discovers `Community Server Browser (0.10.11)`
- panel renders and live community data loads
- TreeStats counts merge and sort correctly
- passwords are absent from plugin JSON and persisted only in Windows Credential Manager

## Important Chorizite version constraint

Use **Chorizite 0.0.15**, not GitHub's nominal latest 0.0.18.

The official plugin index still pairs Lua 0.0.13, RmlUi 0.0.11, Launcher 0.0.9, and AC 0.0.5 with Chorizite 0.0.15. Those plugins are binary-incompatible with the rendering API in Chorizite 0.0.18 (`IRenderInterface` versus `IRenderer`). Installing 0.0.18 with the indexed plugins produces a blank launcher and `TypeLoadException`/`MissingMethodException` errors.

Chorizite also expects `%LOCALAPPDATA%\Temp\chorizite` to exist because its assembly load context enumerates that directory before creating it. The launcher has been run successfully with that directory present.

## Purpose

Replace Chorizite's manual `host:port` entry with a browsable public-server interface. Selecting a server supplies its endpoint directly to `ILauncherBackend.LaunchClient(...)`.

## Data sources

### Community server list

`https://raw.githubusercontent.com/acresources/serverslist/master/Servers.xml`

This is the community list used by current ThwargLauncher releases. Schema fields consumed:

- `id`
- `name`
- `description`
- `emu`
- `server_host`
- `server_port`
- `type`
- `status`
- `website_url`
- `discord_url`

Server-list edits are made through:

`https://github.com/acresources/serverslist`

### Optional player counts

`http://treestats.net/player_counts-latest.json`

Counts are matched to server names case-insensitively. Counts are optional and never block browsing or launching.

### Cache

Last successful responses are cached under:

`C:\Games\Chorizite\data\ServerBrowser\cache`

Files:

- `servers.xml`
- `player-counts.json`

If the network fetch fails, cached community XML is used. Player-count failure is ignored.

## Current features

- automatic feed loading at panel startup
- cached offline/failure fallback
- population-first sorting when counts are available
- text search across name, description, and server type
- server cards showing emulator, PvE/PvP type, status, and player count
- three-region server cards: flexible text column, vertical badge column, stats cube
- endpoint shown in parentheses directly beside each server title
- website and Discord icon buttons in a centred column just left of the badges
- emulator/type/status stacked vertically in a 72px column at 10px type
- population and ping in a bordered 66px cube, ping in its own band beneath a divider
- centered title and separate Servers/Accounts tabs
- ICMP latency shown beside population (`N/A` when the host blocks ping)
- red `Offline` instead of `N/A` when the host name no longer resolves
- toggleable star favorites persisted by server ID
- favorites pinned above the feed as compact single-line cards
- manual favorite ordering through per-row up/down arrows persisted in `favoriteOrder`
- color-coded metadata:
  - PvE: light blue
  - PvP: red
  - Stable: green
  - Development: yellow
  - Experimental: orange
- clickable Discord invite badge when a URL exists
- muted same-width placeholder when Discord is unavailable
- clickable `Web` badge opening the server website via the shared external-URL path
- website badge hidden entirely when no distinct website URL exists
- website values that are really Discord invites fold into the Discord badge
- multiple saved accounts with aliases and default servers
- checked-account launch against either the selected server or account defaults
- compact primary `Launch` action; with no saved accounts it opens the account setup tab
- global default client path and per-server alternate-client overrides
- Windows Credential Manager password storage
- explicit password-protected encrypted backup/import
- client path, last endpoint, favorites, and alternate-client persistence
- direct launch through Chorizite's `ILauncherBackend`
- generic placeholders for sparse-but-launchable community entries

## Sparse listing behavior

A listing is accepted if it has a non-empty host and a valid port from 1–65535. Missing display metadata is normalized:

- missing ID → endpoint (`host:port`)
- missing name → `Unnamed server`
- missing description → guidance linking maintainers to `github.com/acresources/serverslist`
- missing emulator → `Unknown`
- missing type/status → `Unspecified`

Using endpoint as a fallback ID is important: blank/duplicate IDs can cause ambiguous selection and virtual-DOM patch behavior.

## Architecture

### C# plugin/runtime layer

- `src/ServerBrowser/ServerBrowserPlugin.cs`
  - Chorizite assembly plugin entry point
  - launcher-only environment
  - creates the RmlUi panel
  - exposes `RefreshServers()`, `GetDefaultClientPath()`, and `Launch(...)` to Lua

- `src/ServerBrowser/Feeds/ServerFeedClient.cs`
  - `HttpClient` fetching
  - user agent and timeout
  - cache reads/writes
  - optional count failure handling

- `src/ServerBrowser/Feeds/ServerFeedParser.cs`
  - XML parsing and validation
  - sparse field normalization
  - TreeStats JSON parsing
  - count merge and population sorting

- `src/ServerBrowser/Feeds/ServerListing.cs`
- `src/ServerBrowser/Feeds/PlayerCount.cs`
- `src/ServerBrowser/DiscordLink.cs`, `WebsiteLink.cs`, `ExternalLink.cs`
  - scheme/host validation before any shell execute
  - `ExternalLink` is the single `Process.Start` path

- `src/ServerBrowser/Feeds/ServerPingProbe.cs`
  - bounded concurrent ICMP probes
  - unavailable results remain nullable and render as `N/A`

### Account and credential layer

- `src/ServerBrowser/Accounts/AccountManager.cs`
  - non-secret account metadata in `accounts.json`
  - account add/edit/delete and backup orchestration
- `src/ServerBrowser/Accounts/WindowsCredentialStore.cs`
  - generic credentials under `Raajik.Chorizite.ServerBrowser/<account-id>`
  - passwords are never written to plugin JSON files
- `src/ServerBrowser/Accounts/CredentialBackup.cs`
  - AES-256-GCM authenticated encryption
  - PBKDF2-SHA256 with 600,000 iterations and a random salt
  - explicit export/import; backup passphrases are never saved

### Platform reality

Chorizite runs only on Windows: every bundled plugin ships `win-x86`/`win-x64` natives and nothing else, and the framework injects into the Windows AC client. A native Linux host therefore cannot load this plugin at all, which is why CI publishes from `windows-latest`.

The realistic non-Windows case is Wine/Proton, where the process *is* Windows. Wine's `advapi32.spec` exports `CredReadW`, `CredWriteW`, `CredDeleteW`, and `CredFree` as real functions rather than stubs, so the credential store works there.

For hosts that lack those exports anyway, `WindowsCredentialStore.IsAvailable()` probes once with a harmless read and the plugin falls back to `UnavailableSecretStore`. Browsing, favorites, ping, and anonymous launching keep working; only password operations raise a readable `PlatformNotSupportedException`, and account deletion still succeeds. Do not add a Linux keyring backend without a Linux host that can actually load the plugin.

The test suite runs on Linux as well (`.github/workflows/ci.yml`) so nothing hard-binds to a Windows API outside the secret store. The one test that needs Credential Manager carries `[WindowsOnlyFact]` and skips elsewhere.

If the Windows profile is lost, Credential Manager entries are not independently recoverable. The encrypted export is the recovery mechanism, and losing its passphrase makes that backup unrecoverable by design.

### RmlUi/Lua presentation layer

- `src/ServerBrowser/assets/server-browser.rml`
  - panel layout and styling

- `src/ServerBrowser/assets/server-browser.lua`
  - reactive presentation state
  - search
  - selection
  - local settings persistence
  - launch invocation

- `src/ServerBrowser/assets/discord.png` and `web.png`
  - bundled link icons for the title-line buttons
- `src/ServerBrowser/assets/star-on.png` and `star-off.png`
  - bundled favorite icons (the indexed RmlUi font lacks star glyphs)

- `scripts/make_discord_icon.py`, `make_web_icon.py`, `make_star_icons.py`, `make_arrow_icons.py`
  - reproducibly generate the bundled icons (requires Pillow)
  - `make_discord_icon.py` additionally needs `svglib reportlab rlPyCairo pycairo`, because it rasterises Discord's official Clyde path rather than approximating it

Hand-drawn approximations of the Discord mark were tried first and consistently read as a ghost or a pair of speech bubbles at 14px. The silhouette depends on curve detail that primitives cannot reproduce at that size, so the official path is rasterised at 224px, cropped to its ink, and downsampled. Icons are white on transparency; the badge CSS supplies the colour.

## Critical settings-persistence invariant

**Never pass an `rx` state table to `json.encode`.**

`rx:CreateState` moves every value into a C#-backed observable and empties the raw Lua table, leaving only metatable accessors. The bundled `json.lua` first checks `rawget(val, 1) ~= nil or next(val) == nil`; both are false-y for a proxy, so it classifies any state table as an *array*, then iterates it through `__pairs`, hits the string keys, and throws:

```text
json:73: invalid table: mixed or invalid key types
```

Because `saveSettings` had already opened the file with `w`, the throw left `settings.json` truncated to zero bytes, which then broke the next load with `json:185: unexpected character ''`. That combination silently destroyed saved settings whenever a favorite was toggled.

The rules now enforced in `server-browser.lua`:

1. Persist plain Lua tables only — `plainCopy()` deep-copies one level out of any proxy.
2. Favorites persist as the `favoriteOrder` array of server IDs; the `state.favorites` map is rebuilt from it at load.
3. `json.encode` runs inside `pcall` **before** the file is opened, so a failure can never truncate the file.
4. `json.decode` runs inside `pcall`, so an empty or corrupt file falls back to defaults.

Regression coverage: `UiStructureTests.SettingsPersistPlainTablesRatherThanReactiveProxies`.

## Critical RmlUi invariant

**Do not implement filtering by adding/removing server rows from the virtual DOM.**

RmlUi 0.0.11 can crash natively with access violation `0xc0000005` inside:

- `RmlUiNet.Element.SetInnerRml`
- `RmlUi.Lib.RmlUi.VDom.VirtualDom.Patch`

The crash was reproduced when clicking the old ACE filter, which removed many sibling rows in one reactive update.

Current safe approach:

1. Always return one `ServerRow` virtual node for every server.
2. Compute whether it matches search.
3. Toggle the `filtered` CSS class.
4. `.server.filtered { display: none; }`

Regression coverage is in `UiStructureTests.FilteringKeepsEveryServerRowInTheVirtualDom`.

Preserve this stable-child-tree approach for any future filters or sorting UI. If live re-sorting is added, test it carefully—the safest design may be to calculate order only when replacing the entire feed after a network refresh, not on button/key reactions.

Favorite pinning and manual ordering are done in Lua by `pinnedFirst()`, which emits favorites in rank order ahead of everything else. This is a permutation only: every server still yields exactly one row, so the child count never changes and no row is ever added or removed. That is the same class of update as replacing the feed after a network refresh, which is known to be safe.

### RmlUi does not support the CSS `order` property

An earlier 0.5.0 attempt pinned favorites with flexbox `order` to avoid moving rows. RmlUi rejects it outright:

```text
[RmlUi] Syntax error parsing property declaration 'order: -1;'
```

Do not reintroduce `order`, and be skeptical of other modern flexbox properties. `display: flex`, `flex`, `align-items`, and `justify-content` are supported and used throughout.

Badges follow the same rule. The website and Discord icons toggle the `hidden` class rather than being added or removed, so the child list is identical for every server.

### Stylesheet ordering hazard

RmlUi resolves equal-specificity rules by source order, and `.hidden` competes directly with class rules that set `display`. `.tag { display: inline-block; }` silently defeated `.hidden { display: none; }` while `.hidden` was declared near the top, so "hidden" badges still rendered as empty boxes.

`.hidden` is therefore declared **last** in the stylesheet, and `UiStructureTests.HiddenRuleIsDeclaredLastSoItBeatsTagDisplay` pins that ordering. Keep any new `display` rules above it.

## Build, test, and deploy

From the project root:

```bash
./scripts/deploy.sh
```

The script:

1. runs all tests
2. builds the plugin
3. copies DLL/PDB/runtime metadata/manifest/assets to Chorizite

Override the Chorizite installation directory if needed:

```bash
CHORIZITE_HOME='D:/Games/Chorizite' ./scripts/deploy.sh
```

Direct commands:

```bash
dotnet test tests/ServerBrowser.Tests/ServerBrowser.Tests.csproj
dotnet build src/ServerBrowser/ServerBrowser.csproj
```

## Test coverage

`tests/ServerBrowser.Tests/ServerFeedParserTests.cs`

- community XML parsing
- endpoint generation
- case-insensitive TreeStats count merge
- population sorting
- invalid host/port rejection
- sparse listing normalization

`tests/ServerBrowser.Tests/UiStructureTests.cs`

- stable server-row virtual-DOM tree during filtering
- search-only toolbar (removed ACE/GDL/Refresh)
- PvE/PvP and status style predicates
- Discord icon and placeholder slots
- centered full-width server/account tab structure
- inline descriptions, favorite icons, ping labels, and alternate-client controls
- credential-backed account UI operations

`tests/ServerBrowser.Tests/AccountManagerTests.cs`

- metadata/password separation
- Windows Credential Manager round trip and cleanup
- password-protected encrypted backup round trip

`tests/ServerBrowser.Tests/WebsiteLinkTests.cs`

- http/https website acceptance
- rejection of relative, `file:`, `javascript:`, and custom-scheme URLs

`tests/ServerBrowser.Tests/ServerPingProbeTests.cs`

- reachable-host ICMP latency
- population of nullable latency on server listings
- unresolvable hosts reported as `HostResolved = false` with no latency

## Runtime verification

Chorizite logs to:

`C:\Games\Chorizite\data\logs\log.txt`

Successful startup includes lines similar to:

```text
Found 7 plugin manifests: ... Community Server Browser(0.3.0)
Showing document Server Browser C:\Games\Chorizite\plugins\ServerBrowser\assets\server-browser.rml
```

The unrelated `TestPlugin` texture warning comes from Chorizite's plugin index/UI and is not a Server Browser failure.

## Known limitations

- Discord badges open only validated `https://discord.gg/...` links through the Windows default URL handler. The badge click stops propagation so it does not change the selected server.
- Ping uses ICMP rather than the AC game port (the game endpoint is not a TCP listener). Servers that block ICMP correctly show `N/A`.

### Measured ICMP reality of the community list

All 43 unique hosts were probed directly with a 3 second budget and three attempts each:

| Result | Hosts |
| --- | --- |
| ICMP reply | 23 |
| Silent (firewall drops echo) | 15 |
| DNS failure | 3 |
| Destination port unreachable | 2 |

Roughly half the list therefore cannot show a latency number, and that is the servers' behaviour rather than a plugin defect.

The slowest successful reply was **127 ms** and no successful DNS lookup exceeded **106 ms**, so the 750 ms budget in `ServerFeedClient` is already generous. Raising it does not recover any host; the silent ones stay silent at 3 seconds. Do not "fix" missing pings by increasing the timeout.

Hosts that fail DNS are reported separately as `Offline`, because that indicates a genuinely stale listing rather than a firewall policy.

Meaningful reachability for the silent majority would require speaking AC's UDP login handshake the way ThwargLauncher does, which is a much larger protocol job.
- Account backup/export currently uses a typed path rather than a native file-picker dialog.
- TreeStats name matching is exact except for case; aliases such as `ACPrime` versus `Asheron Prime` will not automatically match.
- There is no manual refresh button by design; the panel loads automatically and uses cache fallback.
- Search reacts through RmlUi's `onchange` behavior, which may be commit/focus based rather than every keystroke depending on the control implementation.
- The plugin relies on the older indexed Chorizite stack until official plugins are rebuilt for newer Chorizite core releases.

## Pending user request at this handoff

The repository was clean at tag `v0.8.0` / commit `d24a997`. **All four items below were implemented and verified in 0.9.0** (66/66 tests, deploy succeeded):

1. **12-character backup-passphrase minimum removed.**
   - `CredentialBackup.ValidateMasterPassword` now rejects only empty/whitespace passphrases.
   - UI copy is `Backup passphrase` (no length claim).
   - Regression coverage: `AccountManagerTests.ShortNonEmptyPassphraseCanExportAndImport` and `EmptyOrWhitespacePassphraseIsRejected`.

2. **Server Browser buttons are silent.**
   - `.inner button` in `server-browser.rml` now sets `click-sound: none;`, outranking the global `click-sound: 0x0A0003B3` in `C:\Games\Chorizite\plugins\RmlUi\assets\theme.rcss`.
   - `click-sound` is a Chorizite-registered RCSS property (RmlUi docs' canonical user-defined property; default `none`). Pinned by `ControlsOverrideTheLauncherSkinWithFlatStyling`.

3. **Discord badge moved ahead of emulator/type/status.**
   - The separate `.server-links` column is gone; both link icons fold into the top of `.server-badges` (Discord first, then website, then emulator/type/status), so positions are consistent regardless of which servers have a Discord.
   - Stable virtual-DOM invariant preserved: both icon nodes always exist per row, toggled via `.hidden`.
   - Regression coverage: `UiStructureTests.LinkIconsLiveAtTheTopOfTheBadgeColumnWithDiscordFirst`. Live 800x630 launcher capture still pending.

4. **ThwargLauncher import added.**

**0.9.1 → 0.10.6 iteration (all verified live):**

- **Alternate-client browse dialog**: CLR COM interop cannot run inside Chorizite's collectible plugin ALC ("Typelib export" 0x80131165), and raw IFileOpenDialog vtable calls returned null results (GetResult empirically at slot 16, not 17; even then the result was unreliable). Final solution: `GetOpenFileNameW` from comdlg32 — a single documented P/Invoke, no COM. Runs on a dedicated STA thread.
- **Per-server account pickers**: each server card has a multi-launch checkbox (green), a chevron on favorites, and an expandable picker box. Accounts show as checkbox + per-server favorite star (pins to the top of that server's list, alphabetical within pinned/unpinned groups). Select all/Clear all and an "Alternate Client" toggle live in the picker header; toggling reveals an inline path bar + Browse button. Picks, pins, ticks, and alternate-client paths all persist in settings.json.
- **State-persistence lesson**: `serverAccounts`/`serverAccountFavorites` were saved but never seeded into `rx:CreateState` — a load/save asymmetry that silently wiped them each restart. Any new persisted table must be added to BOTH the load path and the state initializer.
- **Single-handler rule**: nested onclick handlers (row + star) crash the RmlUi plugin's instance cache (`IndexOutOfRangeException` in `RmlInstanceCache`) during event dispatch. One onclick per row inspects `e.TargetElement` classes instead. Also: rx proxy tables report `#table == 0` — all count checks must use `ipairs` counting helpers.
- **Launch button states**: grayed outline = nothing valid; transparent outline = accounts exist but nothing ticked+picked; bright filled red (`#c01818`) = armed (ticked server with picks). Readiness = ticked server with picks, checked via the same rules `beginLaunch` uses.
- **Removed**: drag-to-reorder favorites (distorted drag ghost in RmlUi 0.0.11), the separate Alternate Client tab (folded into pickers), the bottom account-choice strip (superseded by per-server pickers).
- **Misc**: passphrase minimum removed (0.9.0); opaque panel background; centered 780px body; buttons flattened with scoped `click-sound: none`; three-letter search clear; ThwargLauncher import (Accounts.txt + Profiles JSON + UserServerList.xml via redacting schema inspection only — never real values).
   - `src/ServerBrowser/Accounts/ThwargLauncherImporter.cs`: pure parsers (`ThwargLauncherParser`) for `Accounts.txt` (comma-separated key=value rows: `Name`, `Password`, optional `Alias`; header line and `Version=` skipped), `Profiles/*.txt` (JSON `CharacterSettings` → per-account first-character server), and `Servers/UserServerList.xml` (`connect_string` as `host:port`).
   - `ThwargLauncherImporter.Import()` writes metadata through `AccountManager` and passwords only through `ISecretStore`. Collision behavior: an existing saved account with the same normalized username is updated in place (existing default server preserved); imports never touch differently-named accounts. Unmatched Thwarg server names leave the default empty.
   - `ServerBrowserPlugin.ImportThwargLauncher()` reads `%APPDATA%\ThwargLauncher\Accounts.txt` + newest `Profiles/*.txt`, matches Thwarg server names to the live feed by endpoint then name, and returns an "Imported N and updated M" summary. Exposed to Lua and reachable via the `Import from ThwargLauncher` button in the Accounts tab.
   - Fixture-based synthetic tests in `ThwargLauncherImporterTests.cs` (never real launcher data). Real-file schema was inspected with redacting scripts only (key names/lengths, never values).
   - RynLauncher remains unsupported (only log files were ever located; not mined).

**0.10.7 — Accounts tab overhaul (2026-08-29):**

- **Dead controls removed**: "Launch defaults" / "Launch selected" iterated `state.selectedAccounts`, which no UI ever wrote after the per-server pickers landed. `launchCheckedDefaults`, `launchCheckedCurrent`, and `selectedAccounts` all deleted. `beginLaunch`'s fallback now launches the account whose DefaultServerId matches the selected server.
- **Header buttons reveal sections**: Add Account (reveals form, `state.addAccountOpen`), Remove Account (remove mode shows per-row Delete + reorder arrows), Backup (reveals backup/import section, `state.showBackup`). Active sections light the button border via `.actions-active`.
- **Delete is deliberate**: per-row Delete hidden until remove mode. Edit = click the account name row (`editAccount` sets `addAccountOpen`).
- **Last-launch log**: `lastLaunches[accountId] = {when, serverName}` written in `launchAccount` on success, persisted in settings.json (must be seeded in BOTH loadSettings and saveSettings, per the 0.10.4 lesson — it is). Shown as "Last launch: <date> · <server>" / "Never launched" per row; cleared on account delete. No decal plugin involved — launch-time only, no character names.
- **Alternating account-row tints via `.even` class**, NOT `:nth-child` (unsupported, same family as `:not()`).
- Coverage: `AccountsTabHeaderButtonsRevealSectionsAndDeleteIsDeliberate`; list height 170→260px in tests.

**0.10.8 — Accounts tab decoupled from servers (2026-08-29):**

- **Accounts tab no longer touches servers**: `accountDefaultServerId` state, the "Default server" form row, and "Use selected server" are gone; `saveAccount` always passes `''` for DefaultServerId (SaveAccount signature unchanged). `beginLaunch` fallback is now just `launchServerPicks()`.
- **Compact single-line account rows** sized like the servers page: bold gold `.account-alias` span + muted username inline (replaced the stacked H3), `min-height: 24px`.
- **First-run examples**: `AccountManager.SeedExamples()` writes Account1/"Main" + Account2/"Mule/Buffbot" (no passwords) when accounts.json doesn't exist; `ServerBrowserPlugin.Initialize` seeds once. Launching an example surfaces the "no saved password" error, which teaches the flow.
- Test path gotcha: test bin is `tests/ServerBrowser.Tests/bin/Debug/net8.0` — that's **five** `..` hops to repo root, not four; use `Path.GetFullPath`.

**0.10.9 — Accounts tab layout: bottom cards + alphabetical sort (2026-08-29):**

- **Alphabetical sorting**: `orderedAccounts()` sorts by case-insensitive alias. `moveAccount`, `accountOrder` display use, and the arrow controls are gone (reorder was only needed because order was manual).
- **Buttons moved to the very bottom**, below the list; list (`flex: 1` inside `.accounts-wrap`) stretches to fill the tab.
- **Overlapping bottom cards**: Add/Edit, Remove, and Backup each open as an absolutely-positioned `.bottom-card` pinned to the bottom of `.accounts-wrap`, covering the last rows. Exactly one open at a time (`accountsCard()` derives from `removeMode`/`showBackup`/`addAccountOpen`); clicking the same button again closes it (`closeAccountsCard`). Clicking an account name opens the add card in edit mode.
- **Alias color coding**: aliases render green (`#7fdb88`, bold 14px); the form's Alias label mirrors that style (`.field-label-alias`), Username label is small grey (`.field-label-username`) — the label visually maps onto the row text it produces.
- Coverage: `AccountsAreSortedAlphabeticallyWithoutManualReordering`, `AccountsTabBottomButtonsOpenOverlappingCards`, `AccountAliasColorGuidesTheFormFields`.

**0.10.10 — Username-first rows, Alias/Description toggle, full-height tab (2026-08-29):**

- **Username is the primary identity**: row renders Username first (bold gold 14px), alias/description after it. Form field order matches (Username top, `field-lg` at flex 2.2).
- **Alias ↔ Description toggle** in the add/edit card (altToggle pattern). Description mode renders the value small grey (`.account-alias.alias-description`, 11px normal weight); mode inferred per row by `#alias > 20`. Aliases equal to the username are hidden from the row (no duplication).
- **Full-height tab layout**: `body` gets `height: 630px`, `.inner` and `.tabView` are flex columns, so the accounts list stretches to the bottom action bar — no dead space. `.servers` tab keeps its fixed 420px list.
- **RCSS gotcha**: the new `.tabView.hidden` rule tripped `HiddenRuleIsDeclaredLastSoItBeatsTagDisplay` (test finds the FIRST `.hidden { display: none; }`-ish rule vs `.tag`); all `display: none` rules must live in the bottom block.

**0.10.11 — Fix: action bar embedded in first account row (2026-08-29):**

- The 0.10.10 flex chain (`body` 630px → `.inner` → `.tabView` → list `flex: 1`) **broke live**: the launcher window does not give `body` a real height, so the accounts list wrapper collapsed to ~1 row height; the `position: relative` wrapper placed the absolute action bar over account #1 and the list overflowed past it (user screenshot).
- Fix: revert `body`/`.inner`/`.tabView` to auto height; give `.accounts-list` a **fixed height (462px)** so the action bar lands at the window bottom arithmetically. Lesson: in this launcher, an RmlUi `body` is NOT a sized box — never rely on `height: 100%`/`flex: 1` chains from `body`; use explicit pixel heights.
- Regression guard: `FixedWindowLayoutUsesCompactWidthsAndHeights` now pins the 462px list and forbids `body { width: 780px; height:`.

## Pending user request at the previous handoff

(Completed — see above.)

## Suggested next work

Priority order:

1. Consider an AC UDP handshake probe so ICMP-silent servers can still show real reachability.
2. Add tested TreeStats alias mapping for known name mismatches.
3. Add a native file-picker bridge for encrypted account backup import/export.
4. Add recent servers without storing additional credentials.
5. Consider replacing Chorizite's original simple login screen entirely, rather than showing Server Browser as a separate panel, only if this can be done without coupling to private Launcher plugin internals.
6. Package/publish the plugin in Raajik's GitHub repository and optionally submit it to Chorizite's plugin index.

## Repository state at handoff

The project is initialized as a Git repository but changes may still be uncommitted. Before publishing:

```bash
git status
git add .
git commit -m "Add Chorizite community server browser"
```

Do not commit generated `bin/`, `obj/`, test results, or runtime cache files; `.gitignore` excludes them.

## Ownership and attribution

- Author: **Raajik**
- Intended repository: `https://github.com/Raajik/chorizite-server-browser`
- Juggernaut repository: `https://github.com/Raajik/ac-juggernaut`

Treat these as independent products with independent issue tracking and release histories.
