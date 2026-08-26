local rx = require('rx')
local json = require('json')
local plugin = require('Plugins.ServerBrowser')

local SETTINGS_FILE = plugin.DataDirectory .. '/settings.json'

local function loadSettings()
  local result = { username = '', clientpath = '', endpoint = '' }
  local file = io.open(SETTINGS_FILE, 'r')
  if file ~= nil then
    local decoded = json.decode(file:read('a'))
    file:close()
    if decoded ~= nil then result = decoded end
  end
  if result.clientpath == nil or #result.clientpath == 0 then
    result.clientpath = plugin:GetDefaultClientPath()
  end
  result.username = result.username or ''
  result.endpoint = result.endpoint or ''
  return result
end

local saved = loadSettings()
local state = rx:CreateState({
  servers = {},
  selected = nil,
  query = '',
  emulator = 'All',
  loading = true,
  error = '',
  username = saved.username,
  password = '',
  clientpath = saved.clientpath,
  endpoint = saved.endpoint,
  revision = 0
})

local function saveSettings()
  local file = io.open(SETTINGS_FILE, 'w')
  if file == nil then return end
  file:write(json.encode({
    username = state.username,
    clientpath = state.clientpath,
    endpoint = state.endpoint
  }))
  file:close()
end

local function selectServer(server)
  state.selected = server
  state.endpoint = server.Endpoint
  state.revision = state.revision + 1
  saveSettings()
end

local function matches(server)
  local query = string.lower(state.query or '')
  if state.emulator ~= 'All' and server.Emulator ~= state.emulator then return false end
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

local function ServerRow(server)
  return rx:Div({
    class = { server = true, selected = state.selected ~= nil and state.selected.Id == server.Id },
    onclick = function() selectServer(server) end
  }, {
    rx:H3(server.Name, function()
      if server.PlayerCount ~= nil then
        return { rx:Span(tostring(server.PlayerCount), { class = 'count' }) }
      end
      return {}
    end),
    rx:Div((server.Emulator or '?') .. ' | ' .. (server.Type or '?') .. ' | ' .. (server.Status or '?'), { class = 'meta' })
  })
end

local function Details()
  local server = state.selected
  if server == nil then return rx:Div('Select a server to view details.') end
  return rx:Div({
    rx:H3(server.Name),
    rx:Div(server.Endpoint, { class = 'endpoint' }),
    rx:P(server.Description or '', { class = 'description' }),
    rx:P('Emulator: ' .. (server.Emulator or 'Unknown')),
    rx:P('Type: ' .. (server.Type or 'Unknown')),
    rx:P('Status: ' .. (server.Status or 'Unknown')),
    rx:P(server.PlayerCount ~= nil and ('Players: ' .. tostring(server.PlayerCount) .. ' (' .. (server.CountAge or '') .. ')') or 'Players: unavailable'),
    rx:P(#(server.WebsiteUrl or '') > 0 and ('Website: ' .. server.WebsiteUrl) or ''),
    rx:P(#(server.DiscordUrl or '') > 0 and ('Discord: ' .. server.DiscordUrl) or '')
  })
end

local function BrowserView()
  return rx:Div({ revision = state.revision }, {
    rx:H2('Community Server Browser'),
    rx:Div({ class = 'toolbar' }, {
      rx:Input({ type = 'text', value = state.query, placeholder = 'Search name or description...', onchange = function(e) state.query = e.Params.value; state.revision = state.revision + 1 end }),
      rx:Button({ onclick = function() state.emulator = state.emulator == 'ACE' and 'All' or 'ACE'; state.revision = state.revision + 1 end }, 'ACE'),
      rx:Button({ onclick = function() state.emulator = state.emulator == 'GDL' and 'All' or 'GDL'; state.revision = state.revision + 1 end }, 'GDL'),
      rx:Button({ onclick = function() refresh() end }, 'Refresh')
    }),
    rx:Div({ class = 'content' }, {
      rx:Div({ class = 'servers' }, function()
        if state.loading then return { rx:Div('Loading community servers...', { class = 'loading' }) } end
        local rows = {}
        for _, server in ipairs(state.servers) do if matches(server) then rows[#rows + 1] = ServerRow(server) end end
        return rows
      end),
      rx:Div({ class = 'details' }, {
        Details(),
        rx:Div(state.error, { class = 'error' })
      })
    }),
    rx:Div({ class = 'login' }, {
      rx:Div({ class = 'field' }, { rx:Label('Username'), rx:Input({ type = 'text', value = state.username, onchange = function(e) state.username = e.Params.value; saveSettings() end }) }),
      rx:Div({ class = 'field' }, { rx:Label('Password (not saved)'), rx:Input({ type = 'password', value = state.password, onchange = function(e) state.password = e.Params.value end }) }),
      rx:Div({ class = 'field' }, { rx:Label('Client path'), rx:Input({ type = 'text', value = state.clientpath, onchange = function(e) state.clientpath = e.Params.value; saveSettings() end }) }),
      rx:Button({ class = 'launch', onclick = function() plugin:Launch(state.clientpath, state.endpoint, state.username, state.password) end }, 'Launch')
    })
  })
end

document:Mount(function() return BrowserView() end, '#server-browser')
refresh()
