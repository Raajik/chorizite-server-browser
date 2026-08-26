# Chorizite Community Server Browser

<img width="809" height="639" alt="image" src="https://github.com/user-attachments/assets/540a5d06-204b-4342-babf-2d5395a2179b" />
(The interface is still very much a work-in-progress, expect changes!)

A launcher-only Chorizite plugin by [Raajik](https://github.com/Raajik) that browses the AC community server list and launches straight into the server you pick, instead of typing host names and ports.

## Install

Download the latest zip from [Releases](https://github.com/Raajik/chorizite-server-browser/releases) and extract it into your Chorizite `plugins` folder, so the files land in `plugins/ServerBrowser`.

Requires Chorizite 0.0.15 and its official indexed plugins. Chorizite 0.0.18 is binary-incompatible with those UI plugins.

## Features

- browsable community list with search, player counts, and latency
- favorites pinned to the top as compact cards, reorderable by hand
- colour-coded emulator, PvE/PvP, and status badges with website and Discord links
- multiple saved accounts with per-server default client overrides
- passwords stored in Windows Credential Manager, never in plugin files
- optional AES-GCM encrypted account backup protected by a passphrase

Servers are read from the [community list](https://github.com/acresources/serverslist), with optional player counts from [TreeStats](http://treestats.net). Both are cached, and a counts failure never blocks browsing.

## Build

```bash
./scripts/deploy.sh              # test, build, and install locally
CHORIZITE_HOME=D:/Games/Chorizite ./scripts/deploy.sh
```

Architecture, platform constraints, and continuation notes live in [HANDOFF.md](HANDOFF.md).
