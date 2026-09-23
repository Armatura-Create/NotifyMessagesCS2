# NotifyMessages (CS2)

**English** | [Русский](README.ru.md)

[![CI CSSharp](https://github.com/Armatura-Create/NotifyMessagesCS2/actions/workflows/ci-cssharp.yml/badge.svg)](https://github.com/Armatura-Create/NotifyMessagesCS2/actions/workflows/ci-cssharp.yml)
[![CI SwiftlyS2](https://github.com/Armatura-Create/NotifyMessagesCS2/actions/workflows/ci-swiftly.yml/badge.svg)](https://github.com/Armatura-Create/NotifyMessagesCS2/actions/workflows/ci-swiftly.yml)
[![CI Metamod](https://github.com/Armatura-Create/NotifyMessagesCS2/actions/workflows/ci-metamod.yml/badge.svg)](https://github.com/Armatura-Create/NotifyMessagesCS2/actions/workflows/ci-metamod.yml)
[![Release](https://img.shields.io/github/v/release/Armatura-Create/NotifyMessagesCS2?logo=github&color=success)](https://github.com/Armatura-Create/NotifyMessagesCS2/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/Armatura-Create/NotifyMessagesCS2/total?logo=github&color=success)](https://github.com/Armatura-Create/NotifyMessagesCS2/releases)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![CounterStrikeSharp](https://img.shields.io/badge/CounterStrikeSharp-%E2%89%A5%201.0.369-1f6feb?logo=steam)](https://github.com/roflmuffin/CounterStrikeSharp)
[![SwiftlyS2](https://img.shields.io/badge/SwiftlyS2-%E2%89%A5%201.4.9-8957e5)](https://github.com/swiftly-solution/swiftlys2)
[![Platforms](https://img.shields.io/badge/Platforms-Linux%20%7C%20Windows-2ea44f)](#-installation)
[![GeoLite2](https://img.shields.io/badge/GeoLite2-bundled%20%2F%20auto--download-009688)](#-geolite2-data-build-time-download)
[![License](https://img.shields.io/badge/License-GPL--3.0-blue)](LICENSE)

Notifications and advertisements for CS2 servers: chat, center screen (including an HTML panel),
alert and console output, welcome messages, team-change announcements, and a live list of your
other servers via A2S — every message in the player's own language.

Implemented **three times**, for three plugin platforms. They share no code — they share the
**configuration**: `Settings.json`, `Messages.json`, `Ads.json` and `Servers.json` move
between platforms unchanged.

| Platform | Requirements | Archive | Installs into |
|---|---|---|---|
| **CounterStrikeSharp** | CSSharp ≥ 1.0.369 (hence Metamod:Source) | `NotifyMessages_cssharp_<version>.zip` | `addons/counterstrikesharp/plugins/NotifyMessages/` |
| **SwiftlyS2** | SwiftlyS2 ≥ 1.4.9, Metamod **not needed** | `NotifyMessages_swiftly_<version>.zip` | `addons/swiftlys2/plugins/NotifyMessages/` |
| **Metamod:Source** (native C++) | Metamod:Source 2.0 ≥ git1460, **no** CSSharp or SwiftlyS2 | `NotifyMessages_metamod_<linux\|windows>_<version>.zip` | `addons/NotifyMessages/` |

## ✨ Features

- 🔇 **No noise** — side switches at halftime and re-connects after a map change are not announced
- 🌍 **Multi-language** — every message in the player's own language: game interface language first, GeoIP as fallback
- 🎨 **Colored messages** — 20+ color tags mapped straight to CounterStrikeSharp `ChatColors`
- 📱 **Multiple output channels** — chat, center, HTML center, alert, console
- 🔄 **Modular configuration** — four separate config files
- 🖥️ **Server monitoring** — A2S queries with background caching
- 🔌 **Restart notifications** — a command for your external updater, with colors and translations
- ⚡ **Performance-minded** — per-language message caching, lazy placeholder resolution
- 🔒 **Thread-safe** — network polling never touches the game's main thread

## 📦 Installation

1. Download the archive for your platform from the
   [latest release](https://github.com/Armatura-Create/NotifyMessagesCS2/releases/latest)
   and extract it into the root of your game server. The directory layout is already
   inside, together with the GeoLite2 databases.

<details>
<summary>CounterStrikeSharp</summary>

> Requires **CounterStrikeSharp v1.0.369 or newer** — the first release running on .NET 10.
> The plugin is compiled against 1.0.369 on purpose, the minimum it supports, so the build
> itself proves no newer API is used. It runs on any later 1.0.x as well.

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
</details>

<details>
<summary>SwiftlyS2</summary>

> Requires **SwiftlyS2 v1.4.9 or newer**. SwiftlyS2 is its own loader (wired in through
> `gameinfo.gi`), Metamod:Source is not needed. The archive does **not** contain SwiftlyS2's
> own assemblies — the host provides them.

```
addons/swiftlys2/plugins/NotifyMessages/
├── NotifyMessages.dll
├── NotifyMessages.pdb
├── NotifyMessages.deps.json
├── MaxMind.GeoIP2.dll
├── MaxMind.Db.dll
├── GeoLite2-Country.mmdb
└── GeoLite2-City.mmdb
```

Admin commands require the permission `notifymessages.admin` (SwiftlyS2's own permission
system); `sw_restart_notify` is accepted from the server console only.
</details>

<details>
<summary>Metamod:Source (native)</summary>

> Requires **Metamod:Source 2.0 git1460 or newer** — the build that replaced SourceHook with
> KHook. No other framework: no .NET runtime, no CounterStrikeSharp, no SwiftlyS2. There is one
> archive per OS; take the one for your server.

```
addons/
├── metamod/NotifyMessages.vdf
└── NotifyMessages/
    ├── bin/linuxsteamrt64/NotifyMessages.so     (bin/win64/NotifyMessages.dll on Windows)
    ├── GeoLite2-Country.mmdb
    └── GeoLite2-City.mmdb
```

The plugin uses **no signatures and no offsets**: players come from the engine's client hooks,
output goes through factory interfaces, and the game event manager is found by its RTTI class
name. A CS2 update does not break it. Differences from the other two targets:

- admin commands (`mm_restart_notify`, `mm_reload_advert`, `mm_nm_check`, `mm_nm_preview`) run from
  the **server console or rcon** only — Metamod has no permission system of its own;
  `mm_nm_preview` prints the rendered text to the console;
- `Settings.ShowHtmlWhenDead` has no effect (as on SwiftlyS2) — pausing the HTML panel while dead
  would require reading the pawn, that is, engine offsets;
- if the game event manager is not found (reported at load), `CenterHtml` falls back to the plain
  center and team changes are not announced; everything else keeps working.
</details>

2. Start the server — the plugin creates its configuration files automatically.

## ⚙️ Configuration

The plugin uses a **modular configuration** — four separate JSON files. The files are identical
between platforms; only the directory differs:

| Platform | Config directory |
|---|---|
| CounterStrikeSharp | `csgo/addons/counterstrikesharp/configs/plugins/NotifyMessages/` |
| SwiftlyS2 | `csgo/addons/swiftlys2/configs/plugins/NotifyMessages/` |
| Metamod:Source | `csgo/addons/configs/NotifyMessages/` |

```
configs/plugins/NotifyMessages/
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

On SwiftlyS2 the same commands are prefixed `sw_` instead of `css_` (`sw_nm_check`,
`sw_nm_preview`, `sw_reload_advert`), on Metamod:Source — `mm_` (server console or rcon).

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

Same commands on every platform; only the prefix differs. Players type `!servers` in chat
everywhere (`/servers` — the same, without showing the message).

| Action | CounterStrikeSharp | SwiftlyS2 | Metamod:Source |
|---|---|---|---|
| Show the cached server list (10 s cooldown per player) | `css_servers` (player) | `sw_servers` (player) | `mm_servers` (player) |
| Send the `RestartNotify` message for a mark, 0–86400 s | `css_restart_notify <sec>` (server console) | `sw_restart_notify <sec>` (server console) | `mm_restart_notify <sec>` (server console) |
| Reload all four config files without a restart | `css_reload_advert` (`@css/root`) | `sw_reload_advert` (`notifymessages.admin`) | `mm_reload_advert` (server console) |
| Check every template: unknown tags, missing translations | `css_nm_check` (`@css/root`) | `sw_nm_check` (`notifymessages.admin`) | `mm_nm_check` (server console) |
| Render a template: `welcome`, `ad <n>`, `servers`, `key <key>`, `raw <text>` | `css_nm_preview <target>` (`@css/root`) | `sw_nm_preview <target>` (`notifymessages.admin`) | `mm_nm_preview <target>` (server console) |

After the server list is shown, a background cache refresh is started so the next request has
fresh data.

#### Examples:

```
css_restart_notify 300       // RestartNotify message for the 300 s mark
css_nm_preview ad 1          // see the first advertisement block right now
css_reload_advert            // reload every config file
```

---

## 🔌 Updater integration

An external update service usually notifies players with a plain `say <text>` — no colors,
no translations, one language for everyone.

Replace `say` in the updater's config with `css_restart_notify <seconds>` (`sw_restart_notify`
on SwiftlyS2, `mm_restart_notify` on Metamod:Source), and the texts will be pulled from `Messages.json` in each player's own language,
colored per `Settings.json`.

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

The C# targets need the **.NET 10 SDK**; the native one needs a C++17 compiler for its core.
Each target builds on its own:

```bash
export PATH="$HOME/.dotnet:$PATH"

cd cssharp && ./build.sh      # tests + NotifyMessages_cssharp_<version>.zip in bin/Release/net10.0/
cd swiftly && ./build.sh      # tests + NotifyMessages_swiftly_<version>.zip

git submodule update --init --recursive
cd metamod && make -f Makefile.tests -j8 && ./build-tests/nm_tests   # native core, any OS
```

The native plugin itself (hl2sdk, Metamod:Source, protobuf) builds in CI — Steam Runtime 3
container for Linux, `windows-latest` for Windows. [`metamod/README.md`](metamod/README.md) has
the same steps for a local Linux or Docker build.

### Tests:

```bash
dotnet test cssharp/NotifyMessages.sln
dotnet test swiftly/NotifyMessages.sln
cd metamod && make -f Makefile.tests && ./build-tests/nm_tests
```

Coverage focuses on the parts that actually broke: parsing of untrusted A2S packets (truncated
and garbage input), sanitising of remote server names, color tags in every output channel, the
chat-color prefix quirk of the engine, IP extraction (IPv6 included), advertisement rotation,
template diagnostics, language resolution, `restart_notify` template selection, and guards that
the declared minimum API version never drifts above the framework build the plugin is compiled
against.

The SwiftlyS2 tests that need `SwiftlyS2.CS2.dll` run only in an x64 process — that assembly is
x64-only, because a CS2 dedicated server never is anything else. On an arm64 machine they are
skipped with a clear reason; CI runs all of them.

### CI and releases:

- `.github/workflows/ci-cssharp.yml`, `ci-swiftly.yml`, `ci-metamod.yml` — build and test, filtered
  by path: a change in one target does not rebuild the others
- `.github/workflows/release.yml` — on a `v*` tag: one `version` job and one `geoip` job, then all
  three targets in parallel (build → **test** → package → verify the archive), then a GitHub
  Release with all four archives and notes generated from the commits (see [CONTRIBUTING.md](CONTRIBUTING.md))

```bash
git tag v2.3.0 && git push origin v2.3.0
```

The tag is the single source of truth for the version: the workflow derives it from the tag name
(`v2.3.0` → `2.3.0`), passes it to every build, and each plugin reports it to the server —
nothing has to be bumped by hand in the source. The workflow then verifies that the version really
made it into every built binary.

A release is not published if the tests fail. Fresh GeoLite2 databases are downloaded **once** per
release by the `geoip` job and shared with all three targets, so every archive carries the same
edition. The key is the `MAXMIND_LICENSE_KEY` secret of the **`RELEASE` environment** — environment
secrets are visible only to jobs that declare `environment: RELEASE`, and only `geoip` does. Without
the key the archives ship the databases committed under `GeoIP/` (with a warning); with a key that
fails to download, the release fails instead of silently shipping stale data.

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

Resolution order: **the player's own game interface language** (`cl_language`, the same
source SourceMod uses; on SwiftlyS2 it is read synchronously from the client's userinfo
and mapped Steam name → code) → **country by IP** (MaxMind GeoLite2) → `DefaultLang`. Both are matched
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

- CounterStrikeSharp **>= 1.0.369** (`MinimumApiVersion` 369) — or SwiftlyS2 **>= 1.4.9**
  (`MinimumAPIVersion` 1.4.9); .NET 10 in both cases
- or Metamod:Source 2.0 **>= git1460** alone, for the native build
- Linux and Windows

### Why not the SwiftlyS2 menu API for the center panel

SwiftlyS2 ships a screen-menu system. It is deliberately not used for output: a menu is an
exclusive, interactive surface — one active menu per player, so an advertisement would close
whatever menu another plugin has open, and in the server-wide `wasd` input mode it captures
movement keys. Under the hood it renders through the same HTML center panel this plugin already
uses (`CenterHtml`), so the look — title, sizes, colors — is available without those side effects.

### Security

- `css_servers` is available to any player, so it is rate-limited per player and never blocks
  the main thread
- A2S replies are only accepted from the queried address, and every read is bounds-checked
- Text that arrives from a remote server (map names) is sanitised before it enters a template:
  braces, control characters and line breaks are stripped, length is capped — a foreign admin
  cannot inject color tags or multi-line spam into your chat
- Player names substituted into the HTML center panel are HTML-escaped
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
| [libmaxminddb](https://github.com/maxmind/libmaxminddb) (native target) | Apache-2.0 |
| [nlohmann/json](https://github.com/nlohmann/json) (native target) | MIT |
| [doctest](https://github.com/doctest/doctest) (native target, tests only) | MIT |
| [CS2Fixes](https://github.com/Source2ZE/CS2Fixes) — AMBuild scripts and the RTTI vtable lookup, adapted | GPL-3.0 |
| GeoLite2 databases (`GeoIP/*.mmdb`) | [MaxMind GeoLite2 EULA](https://www.maxmind.com/en/geolite2/eula) — **not** covered by this project's GPL |

The GeoLite2 databases shipped in `GeoIP/` and in release archives remain under MaxMind's own
terms. This product includes GeoLite2 data created by MaxMind, available from
[maxmind.com](https://www.maxmind.com).

---

## 💬 Support

[Issues](https://github.com/Armatura-Create/NotifyMessagesCS2/issues) and pull requests are welcome.

Repository: https://github.com/Armatura-Create/NotifyMessagesCS2
