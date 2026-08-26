# Chorizite Community Server Browser

A launcher-only Chorizite plugin by [Raajik](https://github.com/Raajik) that browses the AC community server list and launches the selected server without manually looking up host names or ports.

## Data sources

- Community servers: `https://raw.githubusercontent.com/acresources/serverslist/master/Servers.xml`
- Optional player counts: `http://treestats.net/player_counts-latest.json`

The last successful responses are cached locally. Player-count failure never blocks server browsing.

## Features

- server name, description, emulator, PvE/PvP type, status, website, and Discord
- TreeStats player counts when available
- search and ACE/GDL filtering
- selecting a server automatically chooses its `host:port`
- direct launch through Chorizite
- remembers username, client path, and last server
- **does not save passwords**

## Requirements

Use Chorizite 0.0.15 with its official indexed plugins. Chorizite 0.0.18 is currently binary-incompatible with those UI plugins.

## Build, test, and deploy

```bash
./scripts/deploy.sh
```

Override the install location with `CHORIZITE_HOME` when needed.
