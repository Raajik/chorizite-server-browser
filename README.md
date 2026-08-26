# Chorizite Community Server Browser

A launcher-only Chorizite plugin by [Raajik](https://github.com/Raajik) that browses the AC community server list and launches the selected server without manually looking up host names or ports.

Development status, architecture, known constraints, and continuation notes are documented in [HANDOFF.md](HANDOFF.md).

## Data sources

- Community servers: `https://raw.githubusercontent.com/acresources/serverslist/master/Servers.xml`
- Optional player counts: `http://treestats.net/player_counts-latest.json`

The last successful responses are cached locally. Player-count failure never blocks server browsing.

## Features

- server name, description, emulator, PvE/PvP type, status, website, and Discord
- TreeStats player counts when available
- bounded ICMP latency probes with `N/A` for hosts that block ping
- search across server names and descriptions
- full-width server cards with inline descriptions
- server/account tabs for server-first and account-first launching
- toggleable server favorites
- color-coded PvE/PvP and stability/development status tags
- clickable Discord invite badge or aligned placeholder on every server card
- clickable `Web` badge opening the server's website, or a muted placeholder
- generic guidance for sparse community listings
- per-server alternate client executable overrides
- multiple saved accounts with current-server and default-server launch actions
- passwords stored in Windows Credential Manager, never in settings or account metadata
- optional AES-GCM encrypted account backup protected by a user passphrase
- direct launch through Chorizite

## Requirements

Use Chorizite 0.0.15 with its official indexed plugins. Chorizite 0.0.18 is currently binary-incompatible with those UI plugins.

## Build, test, and deploy

```bash
./scripts/deploy.sh
```

Override the install location with `CHORIZITE_HOME` when needed.
