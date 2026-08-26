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
    alternateClients = {}
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
local savedFavorites = {}
for _, serverId in ipairs(favoriteOrder) do savedFavorites[serverId] = true end

-- rx state tables are C#-backed proxies whose raw contents are empty, so json.encode
-- misreads them as arrays and throws on their string keys. Persist plain copies only.
local function plainCopy(source)
  local copy = {}
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
  selectedAccounts = {},
  accountId = '',
  accountUsername = '',
  accountAlias = '',
  accountPassword = '',
  accountDefaultServerId = '',
  backupPath = plugin.DataDirectory .. '/accounts.csb-backup',
  backupPassword = '',
  revision = 0
})

local function saveSettings()
  local ok, encoded = pcall(json.encode, {
    clientpath = state.clientpath,
    endpoint = state.endpoint,
    favoriteOrder = favoriteOrder,
    alternateClients = plainCopy(state.alternateClients)
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

local function toggleAccount(accountId)
  state.selectedAccounts[accountId] = not state.selectedAccounts[accountId]
  bump()
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

local function ServerRow(server, isFiltered)
  local serverType = string.lower(server.Type or '')
  local status = string.lower(server.Status or '')
  local hasDiscord = #(server.DiscordUrl or '') > 0
  local hasWebsite = #(server.WebsiteUrl or '') > 0
  return rx:Div({
    class = {
      server = true,
      favoriteServer = state.favorites[server.Id] == true,
      selected = state.selected ~= nil and state.selected.Id == server.Id,
      filtered = isFiltered
    },
    onclick = function() selectServer(server) end
  }, {
    rx:Div({ class = 'server-heading' }, {
      rx:Span('', {
        class = { favorite = true, active = state.favorites[server.Id] == true },
        title = state.favorites[server.Id] and 'Remove favorite' or 'Add favorite',
        onclick = function(e) e.StopPropagation(); toggleFavorite(server.Id) end
      }, {
        rx:Img({
          src = state.favorites[server.Id]
            and '@plugins/ServerBrowser/assets/star-on.png'
            or '@plugins/ServerBrowser/assets/star-off.png'
        })
      }),
      rx:Div({ class = 'reorder' }, {
        rx:Span('', {
          class = 'move-favorite',
          title = 'Move favorite up',
          onclick = function(e) e.StopPropagation(); moveFavorite(server.Id, -1) end
        }, { rx:Img({ src = '@plugins/ServerBrowser/assets/arrow-up.png' }) }),
        rx:Span('', {
          class = 'move-favorite',
          title = 'Move favorite down',
          onclick = function(e) e.StopPropagation(); moveFavorite(server.Id, 1) end
        }, { rx:Img({ src = '@plugins/ServerBrowser/assets/arrow-down.png' }) })
      }),
      rx:Div({ class = 'title-block' }, {
        rx:H3(server.Name),
        rx:Span('(' .. server.Endpoint .. ')', { class = 'endpoint' })
      }),
      rx:Div({ class = 'stats' }, {
        rx:Span(server.PlayerCount ~= nil and tostring(server.PlayerCount) or 'N/A', { class = 'count', title = 'Players' }),
        rx:Span(server.PingMs ~= nil and ('Ping: ' .. tostring(server.PingMs) .. ' ms') or 'Ping: N/A', { class = 'ping' })
      })
    }),
    rx:Div({ class = 'server-footer' }, {
      rx:P(server.Description or '', { class = 'description' }),
      rx:Div({ class = 'server-badges' }, {
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
        }),
        rx:Span('Web', {
          class = hasWebsite and 'tag website-badge website-link' or 'tag website-badge hidden',
          title = hasWebsite and ('Open ' .. server.Name .. ' website') or '',
          onclick = hasWebsite and function(e)
            e.StopPropagation()
            plugin:OpenWebsite(server.WebsiteUrl)
          end or nil
        }),
        rx:Span('', {
          class = hasDiscord and 'tag discord-badge discord-icon' or 'tag discord-badge discord-placeholder',
          title = hasDiscord and ('Open ' .. server.Name .. ' Discord') or 'No Discord link provided',
          onclick = hasDiscord and function(e)
            e.StopPropagation()
            plugin:OpenDiscord(server.DiscordUrl)
          end or nil
        }, hasDiscord and { rx:Img({ src = '@plugins/ServerBrowser/assets/discord.png' }) } or {})
      })
    })
  })
end

local function launchAccount(account, server)
  if server == nil then state.error = 'No server is selected for ' .. account.Alias; bump(); return end
  local ok, err = pcall(function()
    plugin:LaunchAccount(account.Id, serverClientPath(server), server.Endpoint)
  end)
  if not ok then state.error = tostring(err); bump() end
end

local function launchCheckedCurrent()
  if state.selected == nil then state.error = 'Select a server first'; bump(); return end
  for _, account in ipairs(state.accounts) do
    if state.selectedAccounts[account.Id] then launchAccount(account, state.selected) end
  end
end

local function hasSelectedAccounts()
  for _, account in ipairs(state.accounts) do
    if state.selectedAccounts[account.Id] then return true end
  end
  return false
end

local function beginLaunch()
  if #state.accounts == 0 then
    state.accountId = ''
    state.accountUsername = ''
    state.accountAlias = ''
    state.accountPassword = ''
    state.accountDefaultServerId = state.selected ~= nil and state.selected.Id or ''
    state.activeTab = 'accounts'
    state.error = ''
    bump()
    return
  end
  launchCheckedCurrent()
end

local function launchCheckedDefaults()
  for _, account in ipairs(state.accounts) do
    if state.selectedAccounts[account.Id] then launchAccount(account, findServer(account.DefaultServerId)) end
  end
end

local function editAccount(account)
  state.accountId = account.Id
  state.accountUsername = account.Username
  state.accountAlias = account.Alias
  state.accountPassword = ''
  state.accountDefaultServerId = account.DefaultServerId or ''
  bump()
end

local function clearAccountForm()
  state.accountId = ''
  state.accountUsername = ''
  state.accountAlias = ''
  state.accountPassword = ''
  state.accountDefaultServerId = state.selected ~= nil and state.selected.Id or ''
end

local function saveAccount()
  local defaultId = state.accountDefaultServerId
  if #defaultId == 0 and state.selected ~= nil then defaultId = state.selected.Id end
  local ok, result = pcall(function()
    return plugin:SaveAccount(
      state.accountId,
      state.accountUsername,
      state.accountAlias,
      defaultId,
      state.accountPassword)
  end)
  if ok then clearAccountForm(); loadAccounts() else state.error = tostring(result); bump() end
end

local function deleteAccount(account)
  local ok, err = pcall(function() plugin:DeleteAccount(account.Id) end)
  if ok then state.selectedAccounts[account.Id] = nil; loadAccounts()
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

local function AccountChoice(account)
  return rx:Button({
    class = { accountChoice = true, checked = state.selectedAccounts[account.Id] == true },
    onclick = function() toggleAccount(account.Id) end
  }, (state.selectedAccounts[account.Id] and '[x] ' or '[ ] ') .. account.Alias)
end

local function ServerLaunchPanel()
  local server = state.selected
  local hasServer = server ~= nil
  local alternate = hasServer and (state.alternateClients[server.Id] or { enabled = false, path = '' }) or { enabled = false, path = '' }
  local choices = {}
  for _, account in ipairs(state.accounts) do choices[#choices + 1] = AccountChoice(account) end
  local canLaunch = hasServer and hasSelectedAccounts()

  return rx:Div({ class = 'launch-panel' }, {
    rx:Div({ class = 'account-choices' }, choices),
    rx:Div({ class = 'client-settings' }, {
      rx:Button({
        class = { toggle = true, enabled = alternate.enabled == true },
        onclick = function() if hasServer then setAlternateEnabled(server, not alternate.enabled) end end
      }, alternate.enabled and 'Use alternate client: ON' or 'Use alternate client: OFF'),
      rx:Input({
        class = { alternatePath = true, hidden = not alternate.enabled },
        type = 'text',
        value = alternate.path or '',
        placeholder = 'Alternate client executable for this server...',
        onchange = function(e) if hasServer then setAlternatePath(server, e.Params.value) end end
      })
    }),
    rx:Button({
      class = { launch = true, disabled = not canLaunch },
      disabled = not canLaunch and #state.accounts > 0,
      onclick = beginLaunch
    }, 'Launch')
  })
end

local function ServersView()
  return rx:Div({ class = { tabView = true, hidden = state.activeTab ~= 'servers' } }, {
    rx:Div({ class = 'toolbar' }, {
      rx:Input({ type = 'text', value = state.query, placeholder = 'Search name or description...', onchange = function(e) state.query = e.Params.value; bump() end })
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

local function AccountRow(account)
  local defaultServer = findServer(account.DefaultServerId)
  return rx:Div({ class = 'account-row' }, {
    AccountChoice(account),
    rx:Div({ class = 'account-name' }, {
      rx:H3(account.Alias),
      rx:Span(account.Username, { class = 'muted' })
    }),
    rx:Span(defaultServer ~= nil and defaultServer.Name or 'No default server', { class = 'account-server' }),
    rx:Button({ onclick = function() editAccount(account) end }, 'Edit'),
    rx:Button({ onclick = function() launchAccount(account, defaultServer) end }, 'Launch default'),
    rx:Button({ onclick = function() launchAccount(account, state.selected) end }, 'Launch selected'),
    rx:Button({ class = 'danger', onclick = function() deleteAccount(account) end }, 'Delete')
  })
end

local function AccountsView()
  local rows = {}
  for _, account in ipairs(state.accounts) do rows[#rows + 1] = AccountRow(account) end
  if #rows == 0 then rows[1] = rx:Div('No saved accounts yet.', { class = 'muted empty' }) end
  return rx:Div({ class = { tabView = true, hidden = state.activeTab ~= 'accounts' } }, {
    rx:Div({ class = { ['account-actions'] = true, hidden = #state.accounts == 0 } }, {
      rx:Button({ onclick = launchCheckedDefaults }, 'Launch defaults'),
      rx:Button({ onclick = launchCheckedCurrent }, 'Launch selected')
    }),
    rx:Div({ class = 'accounts-list' }, rows),
    rx:Div({ class = 'account-form' }, {
      rx:H3(#state.accountId > 0 and 'Edit account' or 'Add account'),
      rx:Div({ class = 'form-row' }, {
        rx:Div({ class = 'field' }, { rx:Label('Alias'), rx:Input({ type = 'text', value = state.accountAlias, onchange = function(e) state.accountAlias = e.Params.value end }) }),
        rx:Div({ class = 'field' }, { rx:Label('Username'), rx:Input({ type = 'text', value = state.accountUsername, onchange = function(e) state.accountUsername = e.Params.value end }) }),
        rx:Div({ class = 'field' }, { rx:Label(#state.accountId > 0 and 'New password (blank keeps current)' or 'Password'), rx:Input({ type = 'password', value = state.accountPassword, onchange = function(e) state.accountPassword = e.Params.value end }) })
      }),
      rx:Div({ class = 'form-row' }, {
        rx:Span('Default server: ' .. (findServer(state.accountDefaultServerId) ~= nil and findServer(state.accountDefaultServerId).Name or 'none'), { class = 'default-server' }),
        rx:Button({ onclick = function() if state.selected ~= nil then state.accountDefaultServerId = state.selected.Id; bump() end end }, 'Use selected server'),
        rx:Button({ onclick = saveAccount }, 'Save account'),
        rx:Button({ onclick = function() clearAccountForm(); bump() end }, 'Clear')
      })
    }),
    rx:Div({ class = 'settings' }, {
      rx:H3('Client and encrypted credential backup'),
      rx:Div({ class = 'field' }, { rx:Label('Default client path'), rx:Input({ type = 'text', value = state.clientpath, onchange = function(e) state.clientpath = e.Params.value; saveSettings() end }) }),
      rx:Div({ class = 'form-row' }, {
        rx:Div({ class = 'field' }, { rx:Label('Backup file'), rx:Input({ type = 'text', value = state.backupPath, onchange = function(e) state.backupPath = e.Params.value end }) }),
        rx:Div({ class = 'field' }, { rx:Label('Backup passphrase (12+ characters)'), rx:Input({ type = 'password', value = state.backupPassword, onchange = function(e) state.backupPassword = e.Params.value end }) }),
        rx:Button({ onclick = exportAccounts }, 'Export'),
        rx:Button({ onclick = importAccounts }, 'Import')
      })
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
