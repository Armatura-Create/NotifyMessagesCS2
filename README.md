# NotifyMessages (CS2)

**English** | [Русский](README.ru.md)

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![CounterStrikeSharp](https://img.shields.io/badge/CounterStrikeSharp-%E2%89%A5%201.0.369-1f6feb?logo=steam)](https://github.com/roflmuffin/CounterStrikeSharp)
[![Platforms](https://img.shields.io/badge/Platforms-Linux%20%7C%20Windows-2ea44f)](#)
[![Downloads](https://img.shields.io/github/downloads/Armatura-Create/NotifyMessagesCS2/total?logo=github&color=success)](https://github.com/Armatura-Create/NotifyMessagesCS2/releases)
[![Release](https://img.shields.io/badge/Release-ZIP%20package-success)](#-build-and-packaging)
[![GeoLite2](https://img.shields.io/badge/GeoLite2-Auto--download-009688)](#-geolite2-data-build-time-download)

A general-purpose notification and advertisement plugin for CounterStrikeSharp / CS2: chat,
center screen (including HTML), alert and console output, plus announcements of other servers
via A2S queries.

## ✨ Features

- 🌍 **Multi-language** — automatic language detection via GeoIP (5+ languages)
- 🎨 **Colored messages** — 20+ color tags mapped straight to CounterStrikeSharp `ChatColors`
- 📱 **Multiple output channels** — chat, center, HTML center, alert, console
- 🔄 **Modular configuration** — four separate config files
- 🖥️ **Server monitoring** — A2S queries with background caching
- 🔌 **Restart notifications** — a command for your external updater, with colors and translations
- ⚡ **Performance-minded** — per-language message caching, lazy placeholder resolution
- 🔒 **Thread-safe** — network polling never touches the game's main thread

## 📦 Installation

> **Requires CounterStrikeSharp v1.0.369 or newer.** v1.0.369 is the first release running on
> .NET 10, and this plugin targets `net10.0` — it will not load on older CounterStrikeSharp builds.
>
> The plugin is compiled against 1.0.369 on purpose — the minimum it supports — so the build itself
> proves no newer API is used. It runs on any later 1.0.x as well.

1. Install [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) (>= v1.0.369) and Metamod:Source
2. Download `NotifyMessages.zip` from the releases page, or build it yourself
3. Extract the archive into the root of your game server

The archive already has the right layout:

```
addons/counterstrikesharp/plugins/NotifyMessages/
├── NotifyMessages.dll
├── NotifyMessages.pdb
├── NotifyMessages.deps.json
├── MaxMind.GeoIP2.dll
├── MaxMind.Db.dll
├── GeoLite2-Country.mmdb
└── GeoLite2-City.mmdb
```

4. Start the server — the plugin creates its configuration files automatically

## ⚙️ Configuration

The plugin uses a **modular configuration** — four separate JSON files in:

```
csgo/addons/counterstrikesharp/configs/plugins/NotifyMessages/
├── Settings.json    # Core plugin settings
├── Messages.json    # All translations and message texts
├── Ads.json         # Advertisements
├── Servers.json     # Servers to monitor
├── *.schema.json    # JSON Schema for each file
└── README.txt       # Short cheat sheet
```

**On first run** the plugin creates all four files with examples. The `*.schema.json` files and
`README.txt` are rewritten on **every** load, so they never describe an older version than the
plugin you are running.

**Open a config in an editor that understands JSON Schema** (VS Code and most others): it will
autocomplete field names, offer the allowed values and highlight typos while you type. That
replaces most of the documentation below — and, unlike documentation, it cannot silently go stale.

After editing, check and preview without waiting for anything:

```
css_nm_check              // unknown tags, gaps in translations — with file and key
css_nm_preview welcome    // show the welcome message to yourself, right now
css_nm_preview ad 1       // show the first advertisement block
css_reload_advert         // apply all four files
```

**A broken config will not take the plugin down.** If a file fails to parse, the plugin logs the
file name, line and position of the error, falls back to defaults *for that file only*, and keeps
running — the other three files are still read normally. The broken file is never overwritten, so
your edits are safe. Trailing commas and `//` comments are accepted on purpose.

```
[Config] Settings.json: ошибка в JSON — строка 3, позиция 2. Файл: .../Settings.json.
         Весь файл проигнорирован, используются значения по умолчанию.
```

---

### 📄 Settings.json — core settings

**Purpose:** base plugin parameters, welcome message, references to translation keys.

| Parameter | Type | Description |
|-----------|------|-------------|
| `Debug` | bool | Verbose logging. **Off by default** — it logs SteamIDs, names and geo data |
| `DefaultLang` | string | Fallback language (RU/US/UA/PL/DE) |
| `PrintToCenterHtml` | bool? | **Deprecated.** Promotes every `Center` message to `CenterHtml`. Set `"MessageType": "CenterHtml"` where you need markup instead |
| `ShowHtmlWhenDead` | bool? | Show HTML to dead players |
| `HtmlCenterDuration` | float? | HTML display duration in seconds (default 5) |
| `WelcomeMessage` | object | Message shown on connect |
| `ChangeTeamMessage` | string | Team change template |
| `JoinTeamMessage` | string | Team join template |
| `TitleAnnounceServers` | string | Header for the `css_servers` command |
| `RestartNotify` | object | Restart/update notification (see below) |
| `LanguageAliases` | object | Message block → language and country codes that map to it |
| `MapsName` | object | Pretty map names (technical name → display name) |

**💡 Important:** message templates use keys like `{prefix}` and `{welcome_player}` — all
translations live in **Messages.json**.

#### WelcomeMessage:

```json
{
  "MessageType": "Chat", // Chat | Center | CenterHtml | Console | Alert
  "Message": "...",      // Template with keys from Messages.json
  "DisplayDelay": 5      // Delay before showing, in seconds
}
```

**Channels are not interchangeable — they have different grammars:**

| Channel | Colors | Line breaks |
|---------|--------|-------------|
| `Chat` | yes, via `{RED}` and friends | `\n` |
| `Center` | no — the engine renders plain text, so tags are stripped | `\n` |
| `CenterHtml` | yes, rendered as markup; plus `{BIG}` / `{MEDIUM}` / `{SMALL}` | `\n` |
| `Console` | no | `\n` |
| `Alert` | no | `\n` |

Old numeric values (`0`–`4`) are still read, so existing configs keep working.

#### RestartNotify — restart notification:

The integration point for an external updater (see [Updater integration](#-updater-integration)).

```json
"RestartNotify": {
  "Enabled": true,
  "MessageType": "Chat",
  "DefaultMessage": "{prefix}{RED}{restart_in_seconds}",
  "Thresholds": {
    "300": "{prefix}{RED}{update_available} {DEFAULT}{restart_in_5min}",
    "60":  "{prefix}{RED}{update_available} {DEFAULT}{restart_in_1min}",
    "30":  "{prefix}{RED}{restart_in_30sec}",
    "10":  "{prefix}{RED}{restart_in_10sec}",
    "1":   "{prefix}{RED}{restart_now}"
  }
}
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `Enabled` | bool | Enable handling of `css_restart_notify` |
| `MessageType` | string | Output channel: `Chat`, `Center`, `CenterHtml`, `Console`, `Alert` |
| `DefaultMessage` | string | Template for values not listed in `Thresholds` |
| `Thresholds` | object | Exact marks: `"seconds"` → template |

Extra placeholders: `{SECONDS}` — the number of seconds, `{TIME_RESTART}` — time as `mm:ss`.
Colors and translations work as everywhere else: texts come from `Messages.json`, colors from tags.

**Template selection:** exact match on the number of seconds first, otherwise `DefaultMessage`.
The "nearest" threshold is deliberately not used — saying "in 5 seconds" when 4 remain would be a lie.

---

### 🌍 Messages.json — translations

**Purpose:** a single place for every translatable text.

```json
{
  "LanguageMessages": {
    "key": {
      "RU": "Русский текст",
      "US": "English text",
      "UA": "Український текст",
      "PL": "Polski tekst",
      "DE": "Deutscher Text"
    }
  },
  "JoinMessages": { ... },
  "LeaveMessages": { ... }
}
```

`LanguageMessages` holds every translatable string: `prefix`, `welcome_player`, `welcome_text`,
`reklama_1`…, `changeTeamMessage`, `joinTeamMessage`,
`player`, `connected`, `disconnected`, `announce_servers`, and the `restart_*` keys used by
`RestartNotify`. See the generated file after first run for the full list.

`JoinMessages` / `LeaveMessages` are arrays of messages shown when a player connects or
disconnects, with `{PLAYERNAME}`, `{COUNTRY}` and `{CITY}` available.

---

### 📢 Ads.json — advertisements

Each block has its own interval and its own list of messages, rotated in order:

```json
{
  "Ads": [
    {
      "Interval": 180,
      "Messages": [
        { "Chat": "{prefix}{reklama_1}" },
        { "Chat": "{prefix}{reklama_2}", "Console": "{reklama_2}" }
      ]
    }
  ]
}
```

Output channel keys: `Chat`, `Center`, `Console`. An unknown key is skipped with a debug line;
a block with an empty `Messages` array is skipped at startup instead of crashing its timer.

---

### 🖥️ Servers.json — server monitoring

```json
{
  "Enabled": true,
  "Interval": 60,
  "QueryTimeoutMs": 500,
  "CacheTtlSeconds": 30,
  "List": [
    {
      "Ip": "127.0.0.1",
      "Port": 27015,
      "MessageTemplate": "{LIGHTBLUE}[SERVER 1]{DEFAULT} {SERVER_MAP} | {GREEN}{SERVER_PLAYERS}{DEFAULT}/{SERVER_MAXPLAYERS}",
      "MessageTemplateConsole": "",
      "MaxPlayersFallback": 32
    }
  ]
}
```

| Parameter | Description |
|-----------|-------------|
| `Enabled` | Enable monitoring |
| `Interval` | Polling interval in seconds (minimum 5) |
| `QueryTimeoutMs` | A2S timeout, 1–5000 ms |
| `CacheTtlSeconds` | Cache lifetime, 0–60 s |
| `MaxPlayersFallback` | Slot count shown when the server is offline |

Placeholders for the templates: `{SERVER_IP}`, `{SERVER_PORT}`, `{SERVER_MAP}` (or `OFFLINE`),
`{SERVER_PLAYERS}`, `{SERVER_MAXPLAYERS}`.

#### How it works:

- ✅ **Polling runs on a background thread** — the game's main thread is never blocked
- ✅ **Smart caching** — a TTL cache limits how often servers are re-queried
- ✅ **Background refresh** — after `css_servers` the cache is refreshed for the next request
- ✅ **No overlapping runs** — at most one polling pass at a time
- ✅ **Command cooldown** — `css_servers` is available to a player once every 10 seconds
- ✅ **Untrusted input** — replies are only accepted from the address that was queried,
  parsing is bounds-checked, and strings are decoded as UTF-8

---

## 🎨 Color tags

Codes come straight from CounterStrikeSharp's `ChatColors` — what the game actually renders.

| Tag | Color | Tag | Color |
|-----|-------|-----|-------|
| `{DEFAULT}` / `{WHITE}` | White | `{RED}` | Red |
| `{DARKRED}` | Dark red | `{LIGHTRED}` | Light red |
| `{GREEN}` | Green | `{LIME}` | Lime |
| `{OLIVE}` | Olive | `{YELLOW}` / `{LIGHTYELLOW}` | Yellow |
| `{GOLD}` / `{ORANGE}` | Gold / orange | `{BLUE}` / `{LIGHTBLUE}` | Blue |
| `{DARKBLUE}` | Dark blue | `{PURPLE}` / `{MAGENTA}` | Purple |
| `{LIGHTPURPLE}` | Pink | `{GREY}` / `{GRAY}` | Grey |
| `{SILVER}` / `{BLUEGREY}` | Silver | | |

**Extra tags:** `{SPACE}` — wide space for alignment, `\n` — line break.

> ⚠️ **Before 2.1.0 the table was custom-made and did not match CS2**: `{BLUE}` rendered as
> magenta, `{YELLOW}` as blue, `{LIGHTBLUE}` as green, `{GREY}` as silver, and so on.
> Tags now produce the color they claim. If your config was tuned by eye against the old
> behaviour, review its colors.

---

## 📝 System placeholders

Available in every message:

| Placeholder | Description | Example |
|-------------|-------------|---------|
| `{MAP}` | Current map | de_dust2 or Dust 2 (if listed in `MapsName`) |
| `{TIME}` | Current time | 15:30:45 |
| `{DATE}` | Current date | 26.11.2024 |
| `{SERVERNAME}` | Server hostname | My CS2 Server |
| `{IP}` | Server IP | 192.168.1.100 |
| `{PORT}` | Server port | 27015 |
| `{MAXPLAYERS}` | Max slots | 32 |
| `{PLAYERS}` | Players online | 18 |
| `{TIME_RESTART}` | Time until restart | 05:00 (in commands) |
| `{SECONDS}` | Seconds until restart | 42 (in `css_restart_notify`) |

---

## 🎮 Commands

### For players:

| Command | Description |
|---------|-------------|
| `css_servers` | Show the cached server list (10 s cooldown per player) |

After the list is shown, a background cache refresh is started so the next request has fresh data.

### For administrators:

| Command | Permission | Description |
|---------|------------|-------------|
| `css_restart_notify <sec>` | SERVER_ONLY | Send the `RestartNotify` message for that mark (0–86400) |
| `css_reload_advert` | @css/root | Reload all four config files without a restart |
| `css_nm_check` | @css/root | Check every template: unknown tags, missing translations |
| `css_nm_preview <target>` | @css/root | Render a template to yourself now: `welcome`, `ad <n>`, `servers`, `key <key>`, `raw <text>` |

#### Examples:

```
css_restart_notify 300       // RestartNotify message for the 300 s mark
css_reload_advert            // Reload every config file
```

---

## 🔌 Updater integration

An external update service usually notifies players with a plain `say <text>` — no colors,
no translations, one language for everyone.

Replace `say` in the updater's config with `css_restart_notify <seconds>`, and the texts will be
pulled from `Messages.json` in each player's own language, colored per `Settings.json`.

Example of an updater config:

```json
{
  "restart_countdown": {
    "300": "css_restart_notify 300",
    "60":  "css_restart_notify 60",
    "30":  "css_restart_notify 30",
    "10":  "css_restart_notify 10",
    "1":   "css_restart_notify 1"
  }
}
```

The updater's marks and the plugin's `RestartNotify.Thresholds` do not have to match: for an
unknown mark the plugin falls back to `DefaultMessage` with `{SECONDS}` substituted.

---

## ⚡ Performance notes

0. **No work when there is nothing to do**
   - `OnTick` returns immediately while no HTML message is active
   - System placeholders (`{SERVERNAME}`, `{PLAYERS}`, `{MAP}`…) are only resolved when actually present
   - Color tag ordering is computed once at load, not per message

1. **Per-language message caching** — a broadcast is processed once per language, not per player

2. **Compiled regular expressions** — tag patterns are compiled once

3. **Background A2S polling** — queries never block the main thread, and never call a CS2 native
   from the background thread: only UDP, strings, and a dictionary under a lock

4. **Thread-safe state** — session timers and the server cache are guarded by locks

5. **Locale-independent formatting** — numbers and dates use the invariant culture, so a server
   running under an unusual locale renders the same text

---

## 🔧 Build and packaging

Requires the **.NET 10 SDK**.

```bash
dotnet build -c Release
```

Resulting archive: `bin/Release/net10.0/NotifyMessages.zip`, already laid out as
`addons/counterstrikesharp/plugins/NotifyMessages/` — extract it into the server root.

### Tests:

```bash
dotnet test
```

Coverage focuses on the parts that actually broke: parsing of untrusted A2S packets (truncated
and garbage input), color tags, IP extraction (IPv6 included), advertisement rotation, and
`css_restart_notify` template selection, plus a guard that `MinimumApiVersion` never drifts
above the CounterStrikeSharp build the plugin is compiled against.

### CI and releases:

- `.github/workflows/ci.yml` — build and test on every push to `main` and every PR
- `.github/workflows/release.yml` — on a `v*` tag: build → **test** → package → GitHub Release
  with `NotifyMessages.zip` attached

```bash
git tag v2.1.1 && git push origin v2.1.1
```

The tag is the single source of truth for the version: the workflow derives it from the tag name
(`v2.1.1` → `2.1.1`), passes it to the build, and the plugin reports it as its `ModuleVersion` —
nothing has to be bumped by hand in the source. The workflow then verifies that the version really
made it into the built DLL.

A release is not published if the tests fail. The `MAXMIND_LICENSE_KEY` repository secret is
optional — without it the archive ships the GeoLite2 databases committed under `GeoIP/`.

---

## 🌍 GeoLite2 data (build-time download)

To ship fresh `GeoLite2-Country.mmdb` and `GeoLite2-City.mmdb` in a release:

**Option 1 — environment variable (recommended for CI):**

```bash
export MAXMIND_LICENSE_KEY=YOUR_KEY
```

**Option 2 — MSBuild property:**

```bash
dotnet build -c Release -p:GeoLiteLicenseKey=YOUR_KEY
```

**Option 3 — local props file:** copy `Directory.Build.props.example` to `Directory.Build.props`
and put your key there. That file is gitignored — **never commit a real key**.

**Fallback:** if the download is skipped or fails, the databases committed under `GeoIP/` are used.

---

## 📚 Notes

### Localization

Resolution order: **the player's own game language** (`cl_language`, which the engine already
knows) → **country by IP** (MaxMind GeoLite2) → `DefaultLang`. Both are matched
case-insensitively, so a client reporting `ru` finds a block named `RU`.

The engine's answer is used first because it is a choice the player made; an IP is a guess about
geography, not about language.

`LanguageAliases` maps extra codes onto an existing block, so one set of translations serves
several countries and language codes:

```json
"LanguageAliases": {
  "RU": ["ru", "kk", "be", "KZ", "BY", "MD"],
  "US": ["en", "GB", "CA", "AU"]
}
```

Without it a player from Kazakhstan falls back to `DefaultLang`, because a `KZ` block does not
exist and duplicating every translation per country is pointless. GeoIP still provides
`{COUNTRY}` and `{CITY}` — that is what it is actually good for.

### Compatibility

- CounterStrikeSharp **>= 1.0.369** (`MinimumApiVersion` 369), .NET 10
- Linux and Windows

### Security

- `css_servers` is available to any player, so it is rate-limited per player and never blocks
  the main thread
- A2S replies are only accepted from the queried address, and every read is bounds-checked
- `Debug` logging is off by default because it writes SteamIDs, names and geo data to the log

---

## 📄 License

Copyright (C) 2025-2026 Armatura

This program is free software: you can redistribute it and/or modify it under the terms of the
GNU General Public License as published by the Free Software Foundation, either version 3 of the
License, or (at your option) any later version.

This program is distributed in the hope that it will be useful, but **without any warranty**;
without even the implied warranty of merchantability or fitness for a particular purpose.
See the [GNU General Public License](LICENSE) for more details.

### Third-party components

| Component | License |
|-----------|---------|
| [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) | MIT |
| [MaxMind.GeoIP2](https://github.com/maxmind/GeoIP2-dotnet) | Apache-2.0 |
| GeoLite2 databases (`GeoIP/*.mmdb`) | [MaxMind GeoLite2 EULA](https://www.maxmind.com/en/geolite2/eula) — **not** covered by this project's GPL |

The GeoLite2 databases shipped in `GeoIP/` and in release archives remain under MaxMind's own
terms. This product includes GeoLite2 data created by MaxMind, available from
[maxmind.com](https://www.maxmind.com).

---

## 💬 Support

[Issues](https://github.com/Armatura-Create/NotifyMessagesCS2/issues) and pull requests are welcome.

Repository: https://github.com/Armatura-Create/NotifyMessagesCS2
