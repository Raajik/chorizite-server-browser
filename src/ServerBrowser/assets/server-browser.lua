local rx = require('rx')
local json = require('json')
local plugin = require('Plugins.ServerBrowser')

local SETTINGS_FILE = plugin.DataDirectory .. '/settings.json'

local function loadSettings()
  local result = {
    clientpath = '',
    endpoint = '',
    favorites = {},
    favoriteOrder = {},
    accountOrder = {},
    alternateClients = {},
    serverAccounts = {},
    serverAccountFavorites = {},
    lastLaunches = {}
  }
  local file = io.open(SETTINGS_FILE, 'r')
  if file ~= nil then
    local contents = file:read('a')
    file:close()
    local ok, decoded = pcall(json.decode, contents)
    if ok and type(decoded) == 'table' then result = decoded end
  end
  if result.clientpath == nil or #result.clientpath == 0 then
    result.clientpath = plugin:GetDefaultClientPath()
  end
  result.endpoint = result.endpoint or ''
  result.favorites = result.favorites or {}
  result.favoriteOrder = result.favoriteOrder or {}
  result.alternateClients = result.alternateClients or {}
  result.serverAccounts = result.serverAccounts or {}
  result.serverAccountFavorites = result.serverAccountFavorites or {}
  result.serverLaunchSelected = result.serverLaunchSelected or {}
  result.lastLaunches = result.lastLaunches or {}
  local cleanOrder = {}
  for _, accountId in ipairs(result.accountOrder or {}) do
    if type(accountId) == 'string' then cleanOrder[#cleanOrder + 1] = accountId end
  end
  result.accountOrder = cleanOrder
  local ranked = {}
  for _, serverId in ipairs(result.favoriteOrder) do ranked[serverId] = true end
  for serverId, isFavorite in pairs(result.favorites) do
    if type(serverId) == 'string' and isFavorite == true and not ranked[serverId] then
      result.favoriteOrder[#result.favoriteOrder + 1] = serverId
    end
  end
  return result
end

local saved = loadSettings()
local favoriteOrder = saved.favoriteOrder
local accountOrder = saved.accountOrder
local savedFavorites = {}
for _, serverId in ipairs(favoriteOrder) do savedFavorites[serverId] = true end
local savedServerAccounts = saved.serverAccounts
local savedServerAccountFavorites = saved.serverAccountFavorites
local serverLaunchSelected = saved.serverLaunchSelected or {}
local lastLaunches = saved.lastLaunches or {}

-- rx state tables are C#-backed proxies whose raw contents are empty, so json.encode
-- misreads them as arrays and throws on their string keys. Persist plain copies only.
local function plainCopy(source)
  local copy = {}
  if source == nil then return copy end
  for key, value in pairs(source) do
    if type(value) == 'table' then
      local inner = {}
      for innerKey, innerValue in pairs(value) do inner[innerKey] = innerValue end
      copy[key] = inner
    else
      copy[key] = value
    end
  end
  return copy
end

local state = rx:CreateState({
  servers = {},
  accounts = {},
  selected = nil,
  activeTab = 'servers',
  query = '',
  loading = true,
  error = '',
  clientpath = saved.clientpath,
  endpoint = saved.endpoint,
  favorites = savedFavorites,
  alternateClients = saved.alternateClients,
  serverAccounts = savedServerAccounts,
  serverAccountFavorites = savedServerAccountFavorites,
  expandedServers = {},
  accountId = '',
  accountUsername = '',
  accountAlias = '',
  accountPassword = '',
  backupPath = plugin.DataDirectory .. '/accounts.csb-backup',
  backupPassword = '',
  removeMode = false,
  showBackup = false,
  addAccountOpen = false,
  revision = 0
})

local function saveSettings()
  local ok, encoded = pcall(json.encode, {
    clientpath = state.clientpath,
    endpoint = state.endpoint,
    favoriteOrder = favoriteOrder,
    accountOrder = accountOrder,
    alternateClients = plainCopy(state.alternateClients),
    serverAccounts = plainCopy(state.serverAccounts or {}),
    serverAccountFavorites = plainCopy(state.serverAccountFavorites or {}),
    serverLaunchSelected = plainCopy(serverLaunchSelected or {}),
    lastLaunches = plainCopy(lastLaunches or {})
  })
  if not ok then
    state.error = 'Could not save settings: ' .. tostring(encoded)
    return
  end
  local file = io.open(SETTINGS_FILE, 'w')
  if file == nil then return end
  file:write(encoded)
  file:close()
end

local function selectServer(server)
  state.selected = server
  state.endpoint = server.Endpoint
  state.revision = state.revision + 1
  saveSettings()
end

local function bump()
  state.revision = state.revision + 1
end

local function toList(items)
  local list = {}
  for i = 0, items.Count - 1 do list[#list + 1] = items[i] end
  return list
end

local function loadAccounts()
  local ok, accounts = pcall(function() return plugin:GetAccounts() end)
  if ok then state.accounts = toList(accounts) else state.error = tostring(accounts) end
  bump()
end

local function favoriteRank(serverId)
  for index = 1, #favoriteOrder do
    if favoriteOrder[index] == serverId then return index end
  end
  return nil
end

local function toggleFavorite(serverId)
  local isFavorite = not state.favorites[serverId]
  state.favorites[serverId] = isFavorite
  local rank = favoriteRank(serverId)
  if isFavorite then
    if rank == nil then favoriteOrder[#favoriteOrder + 1] = serverId end
  elseif rank ~= nil then
    table.remove(favoriteOrder, rank)
  end
  saveSettings()
  bump()
end

local function moveFavorite(serverId, delta)
  local rank = favoriteRank(serverId)
  if rank == nil then return end
  local target = rank + delta
  if target < 1 or target > #favoriteOrder then return end
  favoriteOrder[rank], favoriteOrder[target] = favoriteOrder[target], favoriteOrder[rank]
  saveSettings()
  bump()
end

-- Per-server favorite accounts: pinned above the rest of that server's
-- picker, alphabetical inside the pinned group. Persisted per server.
local function serverAccountFavorites()
  -- Nested rx tables read back nil when untouched; guard like serverAccountPicks.
  local raw = state.serverAccountFavorites
  if type(raw) ~= 'table' then
    state.serverAccountFavorites = {}
    return {}
  end
  return raw
end

local function toggleServerFavorite(serverId, accountId)
  local favs = plainCopy(serverAccountFavorites())
  favs[serverId] = favs[serverId] or {}
  favs[serverId][accountId] = not favs[serverId][accountId] and true or nil
  state.serverAccountFavorites = favs
  saveSettings()
  bump()
end

local function serverAccountPicks()
  local raw = state.serverAccounts
  if type(raw) ~= 'table' then
    state.serverAccounts = {}
    return {}
  end
  return raw
end

-- Per-server account picks: which saved accounts launch on which server.
local function toggleServerAccount(serverId, accountId)
  local picks = plainCopy(serverAccountPicks())
  picks[serverId] = picks[serverId] or {}
  picks[serverId][accountId] = not picks[serverId][accountId] and true or nil
  state.serverAccounts = picks
  saveSettings()
  bump()
end

-- # on an rx proxy table reads 0 (the same reactive-proxy quirk as the
-- settings invariant), so all account-count checks go through this.
local function accountsCount()
  local count = 0
  for _ in ipairs(state.accounts) do count = count + 1 end
  return count
end

local function toggleAllServerAccounts(serverId)
  local picks = plainCopy(serverAccountPicks())
  local current = picks[serverId] or {}
  local count = 0
  for _, account in ipairs(state.accounts) do
    if current[account.Id] then count = count + 1 end
  end
  local target = count < accountsCount()
  local next = {}
  if target then
    for _, account in ipairs(state.accounts) do next[account.Id] = true end
  end
  picks[serverId] = next
  state.serverAccounts = picks
  saveSettings()
  bump()
end

local function serverAccountCount(serverId)
  local count = 0
  for _, account in ipairs(state.accounts) do
    if (serverAccountPicks()[serverId] or {})[account.Id] then count = count + 1 end
  end
  return count
end

-- Accounts are sorted alphabetically by alias (case-insensitive); accountOrder
-- from earlier versions is no longer used for display.
local function orderedAccounts()
  local sorted = {}
  for _, account in ipairs(state.accounts) do sorted[#sorted + 1] = account end
  table.sort(sorted, function(left, right)
    return string.lower(left.Alias or '') < string.lower(right.Alias or '')
  end)
  return sorted
end

local function findServer(serverId)
  for _, server in ipairs(state.servers) do
    if server.Id == serverId then return server end
  end
  return nil
end

local function serverClientPath(server)
  local alternate = state.alternateClients[server.Id]
  if alternate ~= nil and alternate.enabled and #(alternate.path or '') > 0 then
    return alternate.path
  end
  return state.clientpath
end

local function setAlternateEnabled(server, enabled)
  local alternate = state.alternateClients[server.Id] or { enabled = false, path = '' }
  alternate.enabled = enabled
  state.alternateClients[server.Id] = alternate
  saveSettings()
  bump()
end

local function setAlternatePath(server, path)
  local alternate = state.alternateClients[server.Id] or { enabled = false, path = '' }
  alternate.path = path
  state.alternateClients[server.Id] = alternate
  saveSettings()
end

local function setAlternatePathById(serverId, path)
  local alternate = state.alternateClients[serverId] or { enabled = false, path = '' }
  alternate.path = path
  state.alternateClients[serverId] = alternate
  saveSettings()
  bump()
end

local function browseForClientPath(serverId)
  local ok, result = pcall(function() return plugin:BrowseForExecutable() end)
  if ok and type(result) == 'string' and #result > 0 then
    setAlternatePathById(serverId, result)
  elseif not ok then
    state.error = tostring(result)
    bump()
  end
end

local function matches(server)
  local query = string.lower(state.query or '')
  if #query == 0 then return true end
  return string.find(string.lower(server.Name or ''), query, 1, true) ~= nil
    or string.find(string.lower(server.Description or ''), query, 1, true) ~= nil
    or string.find(string.lower(server.Type or ''), query, 1, true) ~= nil
end

local refresh = async(function()
  state.loading = true
  state.error = ''
  local ok, servers = pcall(function() return await(plugin:RefreshServers()) end)
  if ok then
    local list = {}
    for i = 0, servers.Count - 1 do list[#list + 1] = servers[i] end
    state.servers = list
    for _, server in ipairs(list) do
      if server.Endpoint == state.endpoint then state.selected = server end
    end
  else
    state.error = tostring(servers)
  end
  state.loading = false
  state.revision = state.revision + 1
end)

local function pingLabel(server)
  if server.PingMs ~= nil then return 'Ping: ' .. tostring(server.PingMs) .. ' ms' end
  if server.HostResolved == false then return 'Offline' end
  return 'Ping: N/A'
end

-- RmlUi rejects the CSS `order` property, so favorites are ordered here instead.
-- Every server still yields exactly one row; only their sequence changes.
local function pinnedFirst()
  local pinned = {}
  local rest = {}
  for _, server in ipairs(state.servers) do
    local rank = favoriteRank(server.Id)
    if rank ~= nil then
      pinned[#pinned + 1] = { rank = rank, server = server }
    else
      rest[#rest + 1] = server
    end
  end
  table.sort(pinned, function(left, right) return left.rank < right.rank end)
  local ordered = {}
  for _, entry in ipairs(pinned) do ordered[#ordered + 1] = entry.server end
  for _, server in ipairs(rest) do ordered[#ordered + 1] = server end
  return ordered
end

local function AccountPickRow(serverId, account, index)
  local checked = (serverAccountPicks()[serverId] or {})[account.Id] == true
  local fav = (serverAccountFavorites()[serverId] or {})[account.Id] == true
  -- ONE handler on the row. Nested onclicks on the star/checkbox crash the
  -- RmlUi plugin's instance cache during event dispatch (IndexOutOfRange in
  -- RmlInstanceCache), so the row inspects e.TargetElement's classes instead:
  -- clicking the star toggles the pin, anything else toggles the launch pick.
  return rx:Span('', {
    class = { pickRow = true, even = index % 2 == 0, odd = index % 2 == 1 },
    title = 'Click to launch ' .. account.Alias .. ' on this server; click the star to pin it to the top',
    onclick = function(e)
      e.StopPropagation()
      local target = e.TargetElement
      local isStar = false
      while target ~= nil do
        local ok, hasClass = pcall(function() return target:HasClass('pickStar') end)
        if ok and hasClass then isStar = true; break end
        -- stop walking at this row
        local okSelf, isSelf = pcall(function() return target:HasClass('pickRow') end)
        if okSelf and isSelf then break end
        local okParent, parent = pcall(function() return target:GetParentNode() end)
        if not okParent or parent == nil then break end
        target = parent
      end
      if isStar then
        toggleServerFavorite(serverId, account.Id)
      else
        toggleServerAccount(serverId, account.Id)
      end
    end
  }, {
    rx:Span('', { class = { checkbox = true, checked = checked } }),
    rx:Span('', {
      class = { pickStar = true, active = fav },
      title = fav and 'Unpin from this server' or 'Pin to the top of this server'
    }, {
      rx:Img({
        src = '@plugins/ServerBrowser/assets/'
          .. (fav and 'star-on.png' or 'star-off.png')
      })
    }),
    rx:Span(account.Alias, { class = 'pick-label' })
  })
end

-- The expandable account picker for one server. Part of every row so the
-- virtual-DOM child tree stays stable; hidden via CSS when collapsed.
local function ServerAccountPicker(server)
  local picks = {}
  local picksCount = serverAccountCount(server.Id)
  local alternate = state.alternateClients[server.Id] or { enabled = false, path = '' }
  picks[1] = rx:Div({ class = 'picker-head' }, {
    rx:Button({
      onclick = function(e) e.StopPropagation(); toggleAllServerAccounts(server.Id) end
    }, picksCount == accountsCount() and 'Clear all' or 'Select all'),
    rx:Button({
      class = { altToggle = true, checked = alternate.enabled == true },
      title = 'Use an alternate client executable when launching this server',
      onclick = function(e)
        e.StopPropagation()
        setAlternateEnabled(server, not alternate.enabled)
      end
    }, {
      rx:Span('', { class = { checkbox = true, checked = alternate.enabled == true } }),
      rx:Span('Alternate Client', { class = 'altToggle-label' })
    })
  })
  -- Alternate-client path row: hidden until the Alternate Client button is
  -- toggled on. The row always exists (stable DOM); CSS hides it otherwise.
  picks[2] = rx:Div({ class = { altPathRow = true, hidden = alternate.enabled ~= true } }, {
    rx:Input({
      class = 'alternatePath',
      type = 'text',
      value = alternate.path or '',
      placeholder = 'Path to this server\'s client executable...',
      onclick = function(e) e.StopPropagation() end,
      onchange = function(e) setAlternatePath(server, e.Params.value) end
    }),
    rx:Button({
      onclick = function(e) e.StopPropagation(); browseForClientPath(server.Id) end
    }, 'Browse...')
  })
  -- Alphabetical: pinned (per-server favorite) accounts first, then the
  -- rest; each group sorted by alias.
  local favs = serverAccountFavorites()[server.Id] or {}
  local pinned, rest = {}, {}
  for _, account in ipairs(state.accounts) do
    if favs[account.Id] then pinned[#pinned + 1] = account else rest[#rest + 1] = account end
  end
  local byAlias = function(left, right)
    return string.lower(left.Alias or '') < string.lower(right.Alias or '')
  end
  table.sort(pinned, byAlias)
  table.sort(rest, byAlias)
  local index = 0
  for _, account in ipairs(pinned) do
    index = index + 1
    picks[#picks + 1] = AccountPickRow(server.Id, account, index)
  end
  for _, account in ipairs(rest) do
    index = index + 1
    picks[#picks + 1] = AccountPickRow(server.Id, account, index)
  end
  return rx:Div({
    class = { picker = true, hidden = state.expandedServers[server.Id] ~= true }
  }, picks)
end

-- Multi-launch selection: checkbox next to the star marks servers for a
-- combined launch. Persisted so the setup survives restarts.
-- (serverLaunchSelected itself is declared near the top with the other saved tables.)

local function toggleServerLaunch(serverId)
  serverLaunchSelected[serverId] = not serverLaunchSelected[serverId] and true or nil
  saveSettings()
  bump()
end

local function ServerRow(server, isFiltered)
  local serverType = string.lower(server.Type or '')
  local status = string.lower(server.Status or '')
  local hasDiscord = #(server.DiscordUrl or '') > 0
  local hasWebsite = #(server.WebsiteUrl or '') > 0
  local isFavorite = state.favorites[server.Id] == true
  local launchChecked = serverLaunchSelected[server.Id] == true
  return rx:Div({
    class = {
      server = true,
      favoriteServer = isFavorite,
      selected = state.selected ~= nil and state.selected.Id == server.Id,
      filtered = isFiltered
    },
    onclick = function() selectServer(server) end
  }, {
    rx:Div({ class = 'server-main' }, {
      rx:Div({ class = 'server-heading' }, {
        rx:Span('', {
          class = { checkbox = true, checked = launchChecked },
          title = 'Include this server in a multi-launch',
          onclick = function(e) e.StopPropagation(); toggleServerLaunch(server.Id) end
        }),
        rx:Span('', {
          class = { favorite = true, active = isFavorite },
          title = isFavorite and 'Remove favorite' or 'Add favorite',
          onclick = function(e) e.StopPropagation(); toggleFavorite(server.Id) end
        }, {
          rx:Img({
            src = isFavorite
              and '@plugins/ServerBrowser/assets/star-on.png'
              or '@plugins/ServerBrowser/assets/star-off.png'
          })
        }),
        rx:Span('', {
          class = { chevron = true, hidden = not isFavorite },
          title = 'Show accounts to launch on this server',
          onclick = function(e)
            e.StopPropagation()
            state.expandedServers[server.Id] = not state.expandedServers[server.Id] and true or nil
            bump()
          end
        }, {
          rx:Img({
            src = '@plugins/ServerBrowser/assets/'
              .. (state.expandedServers[server.Id] == true and 'chevron-down.png' or 'chevron-right.png')
          })
        }),
        rx:Div({ class = 'title-block' }, {
          rx:H3(server.Name),
          rx:Span('(' .. server.Endpoint .. ')', { class = 'endpoint' })
        })
      }),
      rx:P(server.Description or '', { class = 'description' })
    }),
    rx:Div({ class = 'server-badges' }, {
      rx:Span('', {
        class = hasDiscord and 'tag link-badge discord-icon' or 'tag link-badge hidden',
        title = hasDiscord and ('Open ' .. server.Name .. ' Discord') or '',
        onclick = hasDiscord and function(e)
          e.StopPropagation()
          plugin:OpenDiscord(server.DiscordUrl)
        end or nil
      }, { rx:Img({ src = '@plugins/ServerBrowser/assets/discord.png' }) }),
      rx:Span('', {
        class = hasWebsite and 'tag link-badge website-link' or 'tag link-badge hidden',
        title = hasWebsite and ('Open ' .. server.Name .. ' website') or '',
        onclick = hasWebsite and function(e)
          e.StopPropagation()
          plugin:OpenWebsite(server.WebsiteUrl)
        end or nil
      }, { rx:Img({ src = '@plugins/ServerBrowser/assets/web.png' }) }),
      rx:Span(server.Emulator or 'Unknown', { class = 'tag emulator' }),
      rx:Span(server.Type or 'Unspecified', {
        class = { tag = true, pve = serverType == 'pve', pvp = serverType == 'pvp' }
      }),
      rx:Span(server.Status or 'Unspecified', {
        class = {
          tag = true,
          statusStable = status == 'stable',
          statusDevelopment = status == 'development',
          statusExperimental = status == 'experimental'
        }
      })
    }),
    rx:Div({ class = 'stats-cube' }, {
      rx:Span(server.PlayerCount ~= nil and tostring(server.PlayerCount) or 'N/A', { class = 'count', title = 'Players' }),
      rx:Span(pingLabel(server), {
        class = { ping = true, offline = server.HostResolved == false },
        title = server.HostResolved == false
          and 'This host name no longer resolves, so the listing looks dead'
          or 'ICMP latency; N/A means the host does not answer pings'
      })
    }),
    -- Picker last so flex-wrap places it on its own full-width line beneath
    -- the untouched card when expanded.
    ServerAccountPicker(server)
  })
end

local function launchAccount(account, server)
  if server == nil then state.error = 'No server is selected for ' .. account.Alias; bump(); return end
  local ok, err = pcall(function()
    plugin:LaunchAccount(account.Id, serverClientPath(server), server.Endpoint)
  end)
  if not ok then
    state.error = tostring(err)
  else
    -- Local launch log: timestamp + server name, persisted in settings.
    lastLaunches[account.Id] = {
      when = os.time(),
      serverName = server.Name or server.Endpoint
    }
    saveSettings()
  end
  bump()
end

-- Multi-launch: every server whose picker has accounts checked launches all
-- of them. Falls back to the single global selection when no server picker
-- has any picks.
local function launchServerPicks()
  local launched = 0
  for _, server in ipairs(state.servers) do
    for _, account in ipairs(state.accounts) do
      if (serverAccountPicks()[server.Id] or {})[account.Id] then
        launchAccount(account, server)
        launched = launched + 1
      end
    end
  end
  return launched
end

local function hasLaunchableSelection()
  -- Ready = at least one server card is TICKED and that server has picked
  -- accounts. Mere picks without a tick don't arm the button.
  for _, server in ipairs(state.servers) do
    if serverLaunchSelected[server.Id] then
      for _, account in ipairs(state.accounts) do
        if (serverAccountPicks()[server.Id] or {})[account.Id] then return true end
      end
    end
  end
  return false
end

local function beginLaunch()
  if accountsCount() == 0 then
    state.accountId = ''
    state.accountUsername = ''
    state.accountAlias = ''
    state.accountPassword = ''
    state.activeTab = 'accounts'
    state.addAccountOpen = true
    state.error = ''
    bump()
    return
  end
  -- Servers ticked with the card checkbox launch only their checked
  -- accounts; each ticked server can have a different set.
  local ticked = 0
  for _, server in ipairs(state.servers) do
    if serverLaunchSelected[server.Id] then
      for _, account in ipairs(state.accounts) do
        if (serverAccountPicks()[server.Id] or {})[account.Id] then
          launchAccount(account, server)
          ticked = ticked + 1
        end
      end
    end
  end
  if ticked > 0 then return end
  launchServerPicks()
end

local function editAccount(account)
  state.accountId = account.Id
  state.accountUsername = account.Username
  state.accountAlias = account.Alias
  state.accountPassword = ''
  state.addAccountOpen = true
  bump()
end

local function clearAccountForm()
  state.accountId = ''
  state.accountUsername = ''
  state.accountAlias = ''
  state.accountPassword = ''
end

local function saveAccount()
  local ok, result = pcall(function()
    return plugin:SaveAccount(
      state.accountId,
      state.accountUsername,
      state.accountAlias,
      '',
      state.accountPassword)
  end)
  if ok then clearAccountForm(); loadAccounts() else state.error = tostring(result); bump() end
end

local function deleteAccount(account)
  local ok, err = pcall(function() plugin:DeleteAccount(account.Id) end)
  if ok then
    lastLaunches[account.Id] = nil
    local rank
    for index, id in ipairs(accountOrder) do
      if id == account.Id then rank = index; break end
    end
    if rank ~= nil then table.remove(accountOrder, rank); saveSettings() end
    loadAccounts()
  else state.error = tostring(err); bump() end
end

local function exportAccounts()
  local ok, err = pcall(function() plugin:ExportAccounts(state.backupPath, state.backupPassword) end)
  state.error = ok and ('Encrypted backup saved to ' .. state.backupPath) or tostring(err)
  state.backupPassword = ''
  bump()
end

local function importAccounts()
  local ok, result = pcall(function() return plugin:ImportAccounts(state.backupPath, state.backupPassword) end)
  state.backupPassword = ''
  if ok then state.accounts = toList(result); state.error = 'Encrypted account backup imported'
  else state.error = tostring(result) end
  bump()
end

local function importThwarg()
  local ok, result = pcall(function() return plugin:ImportThwargLauncher() end)
  if ok then
    loadAccounts()
    state.error = tostring(result)
  else
    state.error = tostring(result)
  end
  bump()
end

local function ServerLaunchPanel()
  local server = state.selected
  local hasServer = server ~= nil
  local canLaunch = hasServer and hasLaunchableSelection()

  -- Account picks live in each server card's expandable picker now, so this
  -- panel is just the primary action.
  return rx:Div({ class = 'launch-panel' }, {
    rx:Div({ class = 'launch-row' }, {
      rx:Button({
        class = { launch = true, disabled = not canLaunch, ready = canLaunch, plain = not canLaunch },
        disabled = not canLaunch and accountsCount() > 0,
        onclick = beginLaunch
      }, 'Launch')
    })
  })
end

local function ServersView()
  return rx:Div({ class = { tabView = true, hidden = state.activeTab ~= 'servers' } }, {
    rx:Div({ class = 'toolbar' }, {
      rx:Input({ type = 'text', value = state.query, placeholder = 'Search name or description...', onchange = function(e) state.query = e.Params.value; bump() end }),
      rx:Span('X', {
        class = { clearSearch = true, hidden = #(state.query or '') == 0 },
        title = 'Clear search',
        onclick = function() state.query = ''; bump() end
      })
    }),
    rx:Div({ class = 'servers' }, function()
      if state.loading then return { rx:Div('Loading community servers...', { class = 'loading' }) } end
      local rows = {}
      -- Keep the virtual-DOM child tree stable. RmlUi 0.0.11 can crash in
      -- SetInnerRml when a filter adds/removes dozens of sibling nodes.
      for _, server in ipairs(pinnedFirst()) do
        rows[#rows + 1] = ServerRow(server, not matches(server))
      end
      return rows
    end),
    ServerLaunchPanel()
  })
end

local function launchLabel(account)
  local entry = lastLaunches[account.Id]
  if entry == nil or entry.when == nil then return 'Never launched' end
  return 'Last launch: ' .. os.date('%b %d, %I:%M %p', entry.when) .. ' \194\183 ' .. (entry.serverName or '')
end

local function AccountRow(account, index)
  return rx:Div({ class = { ['account-row'] = true, even = index % 2 == 0 } }, {
    -- Click the name to edit.
    rx:Div({
      class = 'account-main',
      title = 'Click to edit this account',
      onclick = function() editAccount(account) end
    }, {
      rx:Span(account.Alias, { class = 'account-alias' }),
      rx:Span(account.Username, { class = 'muted' })
    }),
    rx:Span(launchLabel(account), { class = 'account-log' }),
    rx:Button({
      class = { danger = true, hidden = state.removeMode ~= true },
      title = 'Permanently remove this account',
      onclick = function(e) e.StopPropagation(); deleteAccount(account) end
    }, 'Delete')
  })
end

-- Which bottom card is open: 'none', 'add', 'remove', or 'backup'. Exactly one
-- at a time; clicking the same button again closes it. Clicking a name to edit
-- also opens the 'add' card (it doubles as the edit form).
local function accountsCard()
  if state.removeMode then return 'remove' end
  if state.showBackup then return 'backup' end
  if state.addAccountOpen then return 'add' end
  return 'none'
end

local function closeAccountsCard()
  state.removeMode = false
  state.showBackup = false
  state.addAccountOpen = false
end

local function AccountsView()
  local rows = {}
  for index, account in ipairs(orderedAccounts()) do rows[#rows + 1] = AccountRow(account, index) end
  if #rows == 0 then rows[1] = rx:Div('No saved accounts yet.', { class = 'muted empty' }) end
  local card = accountsCard()
  return rx:Div({ class = { tabView = true, hidden = state.activeTab ~= 'accounts' } }, {
    -- Full-height list; the bottom card overlaps its last rows.
    rx:Div({ class = 'accounts-wrap' }, {
      rx:Div({ class = 'accounts-list' }, rows),
      -- Add/edit card: opened by Add Account or by clicking an account name.
      rx:Div({
        class = {
          ['bottom-card'] = true,
          hidden = card ~= 'add'
        }
      }, {
        rx:H3(#state.accountId > 0 and 'Edit account' or 'Add account'),
        rx:Div({ class = 'form-row' }, {
          -- Label styling mirrors the row text it produces: 'Alias' in the
          -- alias color/size, 'Username' in small grey with the live-typed
          -- value shown after it so the mapping is unmistakable.
          rx:Div({ class = 'field' }, {
            rx:Span('Alias', { class = 'field-label-alias' }),
            rx:Input({ type = 'text', value = state.accountAlias, onchange = function(e) state.accountAlias = e.Params.value end })
          }),
          rx:Div({ class = 'field' }, {
            rx:Span('Username', { class = 'field-label-username' }),
            rx:Input({ type = 'text', value = state.accountUsername, onchange = function(e) state.accountUsername = e.Params.value end })
          }),
          rx:Div({ class = 'field' }, {
            rx:Span(#state.accountId > 0 and 'New password (blank keeps current)' or 'Password', { class = 'field-label' }),
            rx:Input({ type = 'password', value = state.accountPassword, onchange = function(e) state.accountPassword = e.Params.value end })
          })
        }),
        rx:Div({ class = 'form-row' }, {
          rx:Button({ onclick = function() saveAccount(); closeAccountsCard(); bump() end }, 'Save account'),
          rx:Button({ onclick = function() clearAccountForm(); closeAccountsCard(); bump() end }, 'Cancel')
        })
      }),
      -- Remove card: instructions + per-row Delete buttons are already visible
      -- while remove mode is on.
      rx:Div({
        class = {
          ['bottom-card'] = true,
          hidden = card ~= 'remove'
        }
      }, {
        rx:H3('Remove accounts'),
        rx:Div('Delete buttons are shown on each account row. Click the button again to go back.', { class = 'muted' }),
        rx:Div({ class = 'form-row' }, {
          rx:Button({
            onclick = function() closeAccountsCard(); bump() end
          }, 'Done Removing')
        })
      }),
      -- Backup card: client path + encrypted credential backup/import.
      rx:Div({
        class = {
          ['bottom-card'] = true,
          hidden = card ~= 'backup'
        }
      }, {
        rx:H3('Client and encrypted credential backup'),
        rx:Div({ class = 'form-row' }, {
          rx:Button({ onclick = importThwarg }, 'Import from ThwargLauncher')
        }),
        rx:Div({ class = 'field' }, { rx:Label('Default client path'), rx:Input({ type = 'text', value = state.clientpath, onchange = function(e) state.clientpath = e.Params.value; saveSettings() end }) }),
        rx:Div({ class = 'form-row' }, {
          rx:Div({ class = 'field' }, { rx:Label('Backup file'), rx:Input({ type = 'text', value = state.backupPath, onchange = function(e) state.backupPath = e.Params.value end }) }),
          rx:Div({ class = 'field' }, { rx:Label('Backup passphrase'), rx:Input({ type = 'password', value = state.backupPassword, onchange = function(e) state.backupPassword = e.Params.value end }) }),
          rx:Button({ onclick = exportAccounts }, 'Export'),
          rx:Button({ onclick = importAccounts }, 'Import')
        })
      })
    }),
    -- Action bar pinned at the very bottom, below the list.
    rx:Div({ class = 'account-actions' }, {
      rx:Button({
        class = { ['actions-active'] = card == 'add' },
        onclick = function()
          if card == 'add' then
            clearAccountForm()
            closeAccountsCard()
          else
            clearAccountForm()
            closeAccountsCard()
            state.addAccountOpen = true
          end
          bump()
        end
      }, 'Add Account'),
      rx:Button({
        class = { ['actions-active'] = card == 'remove' },
        onclick = function()
          local wasRemove = state.removeMode
          closeAccountsCard()
          if not wasRemove then state.removeMode = true end
          bump()
        end
      }, 'Remove Account'),
      rx:Button({
        class = { ['actions-active'] = card == 'backup' },
        onclick = function()
          local wasBackup = state.showBackup
          closeAccountsCard()
          if not wasBackup then state.showBackup = true end
          bump()
        end
      }, 'Backup')
    })
  })
end

local function BrowserView()
  return rx:Div({ revision = state.revision }, {
    rx:H2('Community Server Browser'),
    rx:Div({ class = 'tabs' }, {
      rx:Button({ class = { active = state.activeTab == 'servers' }, onclick = function() state.activeTab = 'servers'; bump() end }, 'Servers'),
      rx:Button({ class = { active = state.activeTab == 'accounts' }, onclick = function() state.activeTab = 'accounts'; bump() end }, 'Accounts')
    }),
    ServersView(),
    AccountsView(),
    rx:Div(state.error, { class = 'error' })
  })
end

document:Mount(function() return BrowserView() end, '#server-browser')
loadAccounts()
refresh()
