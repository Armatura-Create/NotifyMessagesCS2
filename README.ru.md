# NotifyMessages (CS2)

[English](README.md) | **Русский**

[![CI CSSharp](https://github.com/Armatura-Create/NotifyMessagesCS2/actions/workflows/ci-cssharp.yml/badge.svg)](https://github.com/Armatura-Create/NotifyMessagesCS2/actions/workflows/ci-cssharp.yml)
[![CI SwiftlyS2](https://github.com/Armatura-Create/NotifyMessagesCS2/actions/workflows/ci-swiftly.yml/badge.svg)](https://github.com/Armatura-Create/NotifyMessagesCS2/actions/workflows/ci-swiftly.yml)
[![Release](https://img.shields.io/github/v/release/Armatura-Create/NotifyMessagesCS2?logo=github&color=success)](https://github.com/Armatura-Create/NotifyMessagesCS2/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/Armatura-Create/NotifyMessagesCS2/total?logo=github&color=success)](https://github.com/Armatura-Create/NotifyMessagesCS2/releases)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![CounterStrikeSharp](https://img.shields.io/badge/CounterStrikeSharp-%E2%89%A5%201.0.369-1f6feb?logo=steam)](https://github.com/roflmuffin/CounterStrikeSharp)
[![SwiftlyS2](https://img.shields.io/badge/SwiftlyS2-%E2%89%A5%201.4.9-8957e5)](https://github.com/swiftly-solution/swiftlys2)
[![Platforms](https://img.shields.io/badge/Platforms-Linux%20%7C%20Windows-2ea44f)](#-установка)
[![GeoLite2](https://img.shields.io/badge/GeoLite2-bundled%20%2F%20auto--download-009688)](#-geolite2-данные-автозагрузка-при-сборке)
[![License](https://img.shields.io/badge/License-GPL--3.0-blue)](LICENSE)

Уведомления и реклама для серверов CS2: чат, центр экрана (включая HTML-панель), alert и консоль,
приветствия, сообщения о смене команды и живой список ваших других серверов через A2S — каждое
сообщение на языке игрока.

Реализовано **дважды**, под две платформы плагинов. Кода у них общего нет — общая
**конфигурация**: `Settings.json`, `Messages.json`, `Ads.json` и `Servers.json` переносятся
между платформами без правок.

| Платформа | Требования | Архив | Куда ставится |
|---|---|---|---|
| **CounterStrikeSharp** | CSSharp ≥ 1.0.369 (а значит и Metamod:Source) | `NotifyMessages_cssharp_<версия>.zip` | `addons/counterstrikesharp/plugins/NotifyMessages/` |
| **SwiftlyS2** | SwiftlyS2 ≥ 1.4.9, Metamod **не нужен** | `NotifyMessages_swiftly_<версия>.zip` | `addons/swiftlys2/plugins/NotifyMessages/` |

## ✨ Особенности

- 🔇 **Без шума** — смена сторон в halftime и возврат игроков после смены карты не анонсируются
- 🌍 **Мультиязычность** — каждое сообщение на языке игрока: сначала язык интерфейса игры, GeoIP как фолбэк
- 🎨 **Цветные сообщения** — поддержка 15+ цветовых тегов
- 📱 **Множество каналов вывода** — чат, центр экрана, HTML-центр, alert, консоль
- 🔄 **Модульная конфигурация** — 4 отдельных конфига для удобства
- 🖥️ **Мониторинг серверов** — A2S-запросы с асинхронным кешированием
- 🔌 **Оповещение о рестарте** — команда для внешнего апдейтера с цветами и переводами
- ⚡ **Высокая производительность** — кеширование сообщений, оптимизированные регулярные выражения
- 🔒 **Thread-safe** — безопасная работа в многопоточной среде
- 🚀 **Фоновые операции** — все запросы к серверам выполняются асинхронно, без блокировки UI

## 📦 Установка

1. Скачайте архив под свою платформу из
   [последнего релиза](https://github.com/Armatura-Create/NotifyMessagesCS2/releases/latest)
   и распакуйте в корень игрового сервера. Структура каталогов уже внутри, вместе с базами GeoLite2.

<details>
<summary>CounterStrikeSharp</summary>

> Требуется **CounterStrikeSharp v1.0.369 или новее** — первая версия на .NET 10.
> Плагин компилируется именно против 1.0.369 — минимальной поддерживаемой версии, — поэтому сама
> сборка доказывает, что более новых API в нём нет. На любой свежей 1.0.x он тоже работает.

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

> Требуется **SwiftlyS2 v1.4.9 или новее**. SwiftlyS2 — самостоятельный лоадер (подключается
> через `gameinfo.gi`), Metamod:Source не нужен. В архиве **нет** сборок самого SwiftlyS2 —
> их предоставляет хост.

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

Админские команды требуют право `notifymessages.admin` (система прав самого SwiftlyS2);
`sw_restart_notify` принимается только из консоли сервера.
</details>

2. Запустите сервер — плагин автоматически создаст конфигурационные файлы.

## ⚙️ Конфигурация

Плагин использует **модульную систему конфигурации** — 4 отдельных JSON-файла. Файлы одинаковы
для обеих платформ, различается только каталог:

| Платформа | Каталог конфигов |
|---|---|
| CounterStrikeSharp | `csgo/addons/counterstrikesharp/configs/plugins/NotifyMessages/` |
| SwiftlyS2 | `csgo/addons/swiftlys2/configs/plugins/NotifyMessages/` |

```
configs/plugins/NotifyMessages/
├── Settings.json    # Основные настройки плагина
├── Messages.json    # Все переводы и текстовые сообщения
├── Ads.json         # Рекламные объявления
├── Servers.json     # Список серверов для мониторинга
├── *.schema.json    # JSON Schema для каждого файла
└── README.txt       # Короткая шпаргалка
```

**При первом запуске** плагин создаёт все 4 файла с примерами. Файлы `*.schema.json` и `README.txt`
перезаписываются при **каждой** загрузке — они не могут описывать версию старше той, что стоит.

**Откройте конфиг в редакторе с поддержкой JSON Schema** (VS Code и большинство других): он будет
подсказывать имена полей, допустимые значения и подсвечивать опечатки прямо во время правки.
Это заменяет бо́льшую часть документации ниже — и, в отличие от неё, не устаревает молча.

После правки не нужно ничего ждать:

```
css_nm_check              // неизвестные теги и дыры в переводах — с файлом и ключом
css_nm_preview welcome    // показать приветствие себе прямо сейчас
css_nm_preview ad 1       // показать первый блок рекламы
css_reload_advert         // применить все четыре файла
```

На SwiftlyS2 те же команды идут с префиксом `sw_` вместо `css_` (`sw_nm_check`,
`sw_nm_preview`, `sw_reload_advert`).

**Битый конфиг не роняет плагин.** Если файл не разобрался, в лог пишется имя файла, строка
и позиция ошибки, для этого файла берутся значения по умолчанию, а остальные три читаются
как обычно. Сломанный файл никогда не перезаписывается — ваши правки не потеряются.
Лишние запятые и `//`-комментарии допускаются осознанно.

```
[Config] Settings.json: ошибка в JSON — строка 3, позиция 2. Файл: .../Settings.json.
         Весь файл проигнорирован, используются значения по умолчанию.
```

---

### 📄 Settings.json — Основные настройки

**Назначение:** Базовые параметры плагина, приветственные сообщения, ссылки на ключи переводов.

#### Структура:

```json
{
  "Debug": true,
  "DefaultLang": "RU",
  "PrintToCenterHtml": false,
  "ShowHtmlWhenDead": null,
  "HtmlCenterDuration": null,
  
  "WelcomeMessage": {
    "MessageType": "Chat",
    "Message": "{prefix}{welcome_player} {RED}{PLAYERNAME} {DEFAULT}{welcome_text}",
    "DisplayDelay": 5
  },
  
  "ChangeTeamMessage": "{prefix}{changeTeamMessage}",
  "JoinTeamMessage": "{prefix}{joinTeamMessage}",
  "TitleAnnounceServers": "{prefix}{announce_servers}",
  
  "MapsName": {
    "de_dust2": "Dust 2",
    "de_mirage": "Mirage",
    "de_inferno": "Inferno",
    "de_nuke": "Nuke",
    "de_overpass": "Overpass"
  }
}
```

#### Параметры:

| Параметр | Тип | Описание |
|----------|-----|----------|
| `Debug` | bool | Включить подробное логирование (true/false) |
| `DefaultLang` | string | Язык по умолчанию (RU/US/UA/PL/DE) |
| `PrintToCenterHtml` | bool? | **Устарело.** Поднимает весь `Center` до `CenterHtml`. Указывайте `"MessageType": "CenterHtml"` там, где нужна разметка |
| `ShowHtmlWhenDead` | bool? | Показывать HTML мёртвым игрокам |
| `HtmlCenterDuration` | float? | Длительность показа HTML в секундах |
| `WelcomeMessage` | object | Приветственное сообщение при подключении |
| `ChangeTeamMessage` | string | Шаблон при смене команды |
| `JoinTeamMessage` | string | Шаблон при входе в команду |
| `TitleAnnounceServers` | string | Заголовок для команды !servers |
| `RestartNotify` | object | Оповещение о рестарте/обновлении (см. ниже) |
| `LanguageAliases` | object | Блок переводов → коды языков и стран, которые на него отображаются |
| `MapsName` | object | Красивые названия карт (технич. название → отображаемое) |

**💡 Важно:** В сообщениях используются ключи типа `{prefix}`, `{welcome_player}` и т.д. — все переводы находятся в **Messages.json**!

#### WelcomeMessage:

```json
{
  "MessageType": "Chat", // Chat | Center | CenterHtml | Console | Alert
  "Message": "...",      // Шаблон с ключами из Messages.json
  "DisplayDelay": 5      // Задержка показа в секундах
}
```

**Каналы не взаимозаменяемы — у них разные грамматики:**

| Канал | Цвета | Перенос строки |
|-------|-------|----------------|
| `Chat` | да, теги `{RED}` и прочие | `\n` |
| `Center` | нет — движок рисует обычный текст, теги будут убраны | `\n` |
| `CenterHtml` | да, разметкой; плюс размеры `{BIG}` / `{MEDIUM}` / `{SMALL}` | `\n` |
| `Console` | нет | `\n` |
| `Alert` | нет | `\n` |

Старые числовые значения (`0`–`4`) по-прежнему читаются — существующие конфиги не ломаются.

#### RestartNotify — оповещение о рестарте:

Точка интеграции с внешним апдейтером (см. [Интеграция с апдейтером](#-интеграция-с-апдейтером)).

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

| Параметр | Тип | Описание |
|----------|-----|----------|
| `Enabled` | bool | Включить обработку `css_restart_notify` |
| `MessageType` | string | Канал вывода: `Chat`, `Center`, `CenterHtml`, `Console`, `Alert` |
| `DefaultMessage` | string | Шаблон для секунд, которых нет в `Thresholds` |
| `Thresholds` | object | Точные отсечки: `"секунды"` → шаблон |

Дополнительные плейсхолдеры: `{SECONDS}` — число секунд, `{TIME_RESTART}` — время в `mm:ss`.
Цвета и переводы работают как везде: тексты берутся из `Messages.json`, цвет задаётся тегами.

**Выбор шаблона:** сначала точное совпадение по числу секунд, иначе `DefaultMessage`.
«Ближайшая» отсечка сознательно не подбирается — на 4 секундах показать «через 5 секунд»
было бы неправдой.

---

### 🌍 Messages.json — Переводы и сообщения

**Назначение:** Централизованное хранилище всех текстов и переводов на разные языки.

#### Структура:

```json
{
  "LanguageMessages": {
    "ключ": {
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

#### LanguageMessages — Переводы:

Здесь находятся ВСЕ переводимые тексты плагина. Примеры ключей:

- `prefix` — префикс для всех сообщений
- `welcome_player` — приветствие игрока
- `welcome_text` — текст приветствия
- `reklama_1`, `reklama_2`, ... — тексты рекламы
- `changeTeamMessage`, `joinTeamMessage` — сообщения о командах
- `player`, `connected`, `disconnected` — статусы игроков
- `announce_servers` — заголовок списка серверов

Полный список смотрите в автоматически созданном файле после первого запуска!

#### JoinMessages / LeaveMessages:

Массивы сообщений для показа при подключении/отключении игроков:

```json
{
  "JoinMessages": {
    "RU": [
      "{player} {connected} Страна: {country}, Город: {city}",
      "{connected} Игрок {PLAYERNAME} из {COUNTRY}!"
    ],
    "US": [
      "{player} {connected} Country: {country}, City: {city}"
    ]
  }
}
```

Плагин случайно выбирает одно из сообщений для языка игрока.

**Доступные плейсхолдеры:**
- `{PLAYERNAME}` — имя игрока
- `{COUNTRY}` — страна (через GeoIP)
- `{CITY}` — город (через GeoIP)
- `{player}`, `{connected}`, `{disconnected}` — ключи переводов из LanguageMessages

---

### 📢 Ads.json — Реклама

**Назначение:** Циклические рекламные объявления с настраиваемыми интервалами.

#### Пример:

```json
{
  "Ads": [
    {
      "Interval": 120,
      "Messages": [
        { "Chat": "{prefix}{reklama_1}" },
        { "Center": "!ws • !knife • !gloves • !skins" }
      ]
    },
    {
      "Interval": 180,
      "Messages": [
        { "Chat": "{prefix}{reklama_2}" },
        { "Center": "!viptest - FREE!" }
      ]
    },
    {
      "Interval": 240,
      "Messages": [
        { "Chat": "{prefix}{reklama_3}" }
      ]
    }
  ]
}
```

#### Структура рекламного блока:

| Параметр | Описание |
|----------|----------|
| `Interval` | Интервал показа в секундах (минимум 1) |
| `Messages` | Массив сообщений (показываются циклически) |

#### Каналы вывода:

- `"Chat"` — в чат
- `"Center"` — в центр экрана
- `"Console"` — в консоль игрока

Можно комбинировать несколько каналов в одном сообщении!

**💡 Совет:** Ключи типа `{reklama_1}`, `{reklama_2}` берутся из **Messages.json** → `LanguageMessages`

---

### 🖥️ Servers.json — Мониторинг серверов

**Назначение:** Отображение статуса других серверов через A2S-протокол.

#### Пример:

```json
{
  "Enabled": true,
  "Interval": 60,
  "QueryTimeoutMs": 500,
  "CacheTtlSeconds": 30,
  
  "List": [
    {
      "Ip": "123.45.67.89",
      "Port": 27015,
      "MessageTemplate": "{LIGHTBLUE}[SERVER 1]{DEFAULT} {SERVER_MAP} | {GREEN}{SERVER_PLAYERS}{DEFAULT}/{SERVER_MAXPLAYERS}",
      "MessageTemplateConsole": "Server 1: {SERVER_IP}:{SERVER_PORT} - {SERVER_MAP} | {SERVER_PLAYERS}/{SERVER_MAXPLAYERS}",
      "MaxPlayersFallback": 32
    },
    {
      "Ip": "123.45.67.90",
      "Port": 27015,
      "MessageTemplate": "{LIGHTBLUE}[SERVER 2]{DEFAULT} {SERVER_MAP} | {GREEN}{SERVER_PLAYERS}{DEFAULT}/{SERVER_MAXPLAYERS}",
      "MessageTemplateConsole": "Server 2: {SERVER_IP}:{SERVER_PORT} - {SERVER_MAP} | {SERVER_PLAYERS}/{SERVER_MAXPLAYERS}",
      "MaxPlayersFallback": 32
    }
  ]
}
```

#### Основные параметры:

| Параметр | Тип | Описание |
|----------|-----|----------|
| `Enabled` | bool | Включить/выключить мониторинг (по умолчанию `false`) |
| `Interval` | float | Интервал автоматического опроса в секундах (минимум 5, рекомендуется 60+) |
| `QueryTimeoutMs` | int | Таймаут A2S-запроса в миллисекундах (200-5000, рекомендуется 500) |
| `CacheTtlSeconds` | int | Время жизни кеша в секундах (0-60, рекомендуется 30) |
| `List` | array | Массив серверов для мониторинга |

#### Параметры сервера:

| Параметр | Описание |
|----------|----------|
| `Ip` | IP-адрес или hostname сервера |
| `Port` | Порт сервера |
| `MessageTemplate` | Шаблон для чата (с цветовыми тегами) |
| `MessageTemplateConsole` | Шаблон для консоли (без цветов) |
| `MaxPlayersFallback` | Макс. игроков если сервер оффлайн |

#### Плейсхолдеры для MessageTemplate:

| Плейсхолдер | Описание |
|-------------|----------|
| `{SERVER_IP}` | IP-адрес сервера |
| `{SERVER_PORT}` | Порт сервера |
| `{SERVER_MAP}` | Текущая карта (или "OFFLINE" если недоступен) |
| `{SERVER_PLAYERS}` | Количество игроков онлайн |
| `{SERVER_MAXPLAYERS}` | Максимум игроков (или MaxPlayersFallback) |

#### Особенности работы:

- ✅ **Опрос в фоновом потоке** — главный поток игры не блокируется вообще
- ✅ **Умное кеширование** — TTL-кеш снижает нагрузку на серверы
- ✅ **Фоновое обновление** — после команды `!servers` кеш обновляется для следующего запроса
- ✅ **Защита от наложения** — одновременно идёт максимум один проход опроса
- ✅ **Кулдаун команды** — `css_servers` доступна игроку раз в 10 секунд
- ✅ **Недоверенный ввод** — ответы принимаются только с адреса опрашиваемого сервера,
  разбор пакета проверяет границы буфера, строки читаются как UTF-8

---

## 🎨 Цветовые теги

Плагин поддерживает следующие цветовые теги (автоматически конвертируются в коды CS2):

Коды берутся напрямую из `ChatColors` CounterStrikeSharp — то, что реально рисует игра.

| Тег | Цвет | Тег | Цвет |
|-----|------|-----|------|
| `{DEFAULT}` / `{WHITE}` | Белый | `{RED}` | Красный |
| `{DARKRED}` | Тёмно-красный | `{LIGHTRED}` | Светло-красный |
| `{GREEN}` | Зелёный | `{LIME}` | Лайм |
| `{OLIVE}` | Оливковый | `{YELLOW}` / `{LIGHTYELLOW}` | Жёлтый |
| `{GOLD}` / `{ORANGE}` | Золотой / оранжевый | `{BLUE}` / `{LIGHTBLUE}` | Синий |
| `{DARKBLUE}` | Тёмно-синий | `{PURPLE}` / `{MAGENTA}` | Фиолетовый |
| `{LIGHTPURPLE}` | Розовый | `{GREY}` / `{GRAY}` | Серый |
| `{SILVER}` / `{BLUEGREY}` | Серебряный | | |

> ⚠️ **В версиях до 2.1.0 таблица кодов была своя и не совпадала с CS2**: `{BLUE}` рисовался
> пурпурным, `{YELLOW}` — синим, `{LIGHTBLUE}` — зелёным, `{GREY}` — серебряным и т.д.
> После обновления теги дают заявленный цвет; если конфиг подбирался «на глаз»
> под старое поведение, цвета в нём стоит перепроверить.

**Дополнительные теги:**
- `{SPACE}` — широкий пробел для выравнивания
- `\n` — перенос строки (автоматически конвертируется в `\u2029`)

---

## 📝 Системные плейсхолдеры

Доступны во всех сообщениях:

| Плейсхолдер | Описание | Пример |
|-------------|----------|--------|
| `{MAP}` | Название карты | de_dust2 или DUST 2 (если в MapsName) |
| `{TIME}` | Текущее время | 15:30:45 |
| `{DATE}` | Текущая дата | 26.11.2024 |
| `{SERVERNAME}` | Имя сервера | Мой CS2 Сервер |
| `{IP}` | IP сервера | 192.168.1.100 |
| `{PORT}` | Порт сервера | 27015 |
| `{MAXPLAYERS}` | Макс. слотов | 32 |
| `{PLAYERS}` | Игроков онлайн | 18 |
| `{TIME_RESTART}` | Время до рестарта | 05:00 (в командах) |

---

## 🎮 Команды

Команды одинаковы на обеих платформах, различается только префикс.

| Действие | CounterStrikeSharp | SwiftlyS2 |
|---|---|---|
| Показать список серверов из кеша (кулдаун 10 с на игрока) | `css_servers` (игрок) | `sw_servers` (игрок) |
| Отправить сообщение `RestartNotify` для отсечки, 0–86400 с | `css_restart_notify <сек>` (консоль сервера) | `sw_restart_notify <сек>` (консоль сервера) |
| Перезагрузить все 4 конфига без перезапуска | `css_reload_advert` (`@css/root`) | `sw_reload_advert` (`notifymessages.admin`) |
| Проверить все шаблоны: неизвестные теги, дыры в переводах | `css_nm_check` (`@css/root`) | `sw_nm_check` (`notifymessages.admin`) |
| Показать шаблон себе прямо сейчас: `welcome`, `ad <n>`, `servers`, `key <ключ>`, `raw <текст>` | `css_nm_preview <цель>` (`@css/root`) | `sw_nm_preview <цель>` (`notifymessages.admin`) |

После показа списка серверов запускается фоновое обновление кеша, чтобы следующий запрос
показал актуальные данные.

#### Примеры:

```
css_restart_notify 300       // Оповещение из RestartNotify для отсечки 300 сек
css_nm_preview ad 1          // увидеть первый блок рекламы прямо сейчас
css_reload_advert            // Перезагрузить все конфиги
```

---

## 🔌 Интеграция с апдейтером

Внешний сервис обновления обычно шлёт игрокам голый `say <текст>` — без цветов, без переводов,
одним языком для всех.

Замените в конфиге апдейтера команду `say` на `css_restart_notify <секунды>` (на SwiftlyS2 —
`sw_restart_notify`) — и тексты подтянутся из `Messages.json` на языке каждого игрока,
с цветами из `Settings.json`.

Пример конфига апдейтера:

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

Отсечки в апдейтере и ключи `RestartNotify.Thresholds` в плагине не обязаны совпадать:
для незнакомой отсечки плагин возьмёт `DefaultMessage` с подстановкой `{SECONDS}`.

---

## ⚡ Оптимизации и производительность

### Реализованные улучшения:

0. **Ноль работы, когда работы нет**
   - `OnTick` мгновенно выходит, пока нет активных HTML-сообщений (раньше каждый тик перебирал всех игроков)
   - Системные плейсхолдеры (`{SERVERNAME}`, `{PLAYERS}`, `{MAP}`...) резолвятся только если реально есть в строке
   - Порядок замены цветовых тегов считается один раз при загрузке, а не на каждое сообщение

1. **Кеширование сообщений по языку**
   - Сообщения обрабатываются один раз для каждого языка
   - Если у всех игроков RU язык, обработка происходит только один раз
   - Значительное сокращение CPU нагрузки при большом количестве игроков

2. **Скомпилированные регулярные выражения**
   - Regex для парсинга тегов компилируется один раз при загрузке
   - Значительное ускорение обработки сообщений

3. **Умные A2S-запросы серверов**
   - Запросы полностью вынесены в фоновый поток — главный поток игры не блокируется
   - Из фона не вызывается ни один натив CS2: только UDP, строки и словарь под `lock`
   - TTL-кеширование снижает частоту запросов
   - Повторный запуск во время активного прохода отбрасывается

4. **Thread-safe операции**
   - Все критические секции защищены `lock`
   - SessionService безопасен для многопоточного доступа
   - ServerStatusService использует потокобезопасный кеш
   - Полная защита от race conditions

5. **Улучшенное логирование**
   - Временные метки для всех сообщений (HH:mm:ss)
   - Полные Stack Trace для ошибок в Debug режиме
   - Структурированный формат: `[timestamp] [plugin] [level] message`
   - Опциональный вывод обработанных сообщений в консоль

6. **Модульная конфигурация**
   - 4 отдельных файла для разных функций
   - Быстрая загрузка при старте
   - Простая поддержка и редактирование

---

## 🔧 Build и упаковка

Нужен **.NET 10 SDK**. Каждая цель собирается сама по себе:

```bash
export PATH="$HOME/.dotnet:$PATH"

cd cssharp && ./build.sh      # тесты + NotifyMessages_cssharp_<версия>.zip в bin/Release/net10.0/
cd swiftly && ./build.sh      # тесты + NotifyMessages_swiftly_<версия>.zip
```

### Тесты:

```bash
dotnet test cssharp/NotifyMessages.sln
dotnet test swiftly/NotifyMessages.sln
```

Покрыты чистые части, которые ломались чаще всего: разбор недоверенных A2S-пакетов
(усечённые и мусорные данные), санация чужих имён серверов, цветовые теги во всех каналах
вывода, причуда движка с цветом в начале сообщения, извлечение IP (включая IPv6), ротация
рекламных блоков, диагностика шаблонов, определение языка, выбор шаблона для `restart_notify`,
а также страховки от того, чтобы заявленная минимальная версия API не уехала выше версии
фреймворка, против которой идёт сборка.

Тесты Swiftly-цели, которым нужна `SwiftlyS2.CS2.dll`, выполняются только в x64-процессе —
эта сборка собрана только под x64, потому что выделенный сервер CS2 другим не бывает.
На arm64-машине они пропускаются с внятной причиной, в CI выполняются все.

### CI и релизы:

- `.github/workflows/ci-cssharp.yml`, `ci-swiftly.yml` — сборка и тесты с фильтром по путям:
  правка одной цели не пересобирает другую
- `.github/workflows/release.yml` — по тегу `v*`: одна задача `version`, затем обе цели
  параллельно (сборка → **тесты** → упаковка → проверка архива), затем GitHub Release с обоими
  архивами и описанием, собранным из коммитов (см. [CONTRIBUTING.md](CONTRIBUTING.md))

```bash
git tag v2.3.0 && git push origin v2.3.0
```

Версию задаёт **тег** и только он: workflow вычисляет её из имени тега (`v2.3.0` → `2.3.0`),
передаёт в обе сборки, и каждый плагин сообщает её серверу — в исходниках поднимать ничего
не нужно. Затем workflow проверяет, что версия действительно попала в каждую собранную DLL.

Релиз не публикуется, если тесты красные. Секрет `MAXMIND_LICENSE_KEY` в репозитории
не обязателен — без него в архивы попадут закоммиченные базы из `GeoIP/`.

---

## 🌍 GeoLite2 данные (автозагрузка при сборке)

Чтобы в релиз попадали актуальные базы `GeoLite2-Country.mmdb` и `GeoLite2-City.mmdb`:

### Способ 1: Переменная окружения (рекомендовано для CI)

**macOS/Linux:**
```bash
export MAXMIND_LICENSE_KEY=ВАШ_КЛЮЧ
dotnet build -c Release
```

**Windows (PowerShell):**
```powershell
setx MAXMIND_LICENSE_KEY "ВАШ_КЛЮЧ"
# Перезапустите терминал/IDE
dotnet build -c Release
```

### Способ 2: Свойство MSBuild

```bash
dotnet build -c Release -p:GeoLiteLicenseKey=ВАШ_КЛЮЧ
```

### Способ 3: Локальный props-файл

Скопируйте `Directory.Build.props.example` в `Directory.Build.props` и укажите ключ. Файл исключён из git.

### Фолбэк

Если ключ не задан или загрузка не удалась, сборка использует локальные файлы:
- Положите `GeoLite2-Country.mmdb` и `GeoLite2-City.mmdb` в папку `GeoIP/` в корне репозитория
- Они будут автоматически скопированы в выходную папку и включены в релизный ZIP

**Получить ключ:** Зарегистрируйтесь на [maxmind.com](https://www.maxmind.com/en/geolite2/signup)

---

## 📚 Примечания

### Локализация

Порядок определения: **язык интерфейса игры игрока** (`cl_language` — тот же источник,
что у SourceMod; на SwiftlyS2 читается синхронно из userinfo клиента и переводится из
имени Steam в код) → **страна по IP** (MaxMind GeoLite2) → `DefaultLang`. Регистр не важен: клиент отдаёт `ru`,
а блок в конфиге называется `RU` — совпадёт.

Ответ движка идёт первым, потому что это выбор самого игрока, а IP — догадка о географии,
а не о языке.

`LanguageAliases` отображает дополнительные коды на существующий блок, чтобы один набор
переводов обслуживал несколько стран и языков:

```json
"LanguageAliases": {
  "RU": ["ru", "kk", "be", "KZ", "BY", "MD"],
  "US": ["en", "GB", "CA", "AU"]
}
```

Без этого игрок из Казахстана получал `DefaultLang`: блока `KZ` нет, а дублировать все переводы
под каждую страну бессмысленно. GeoIP остаётся источником `{COUNTRY}` и `{CITY}` — там он и уместен.

### Особенности работы

- Приветственные сообщения поддерживают все плейсхолдеры и локализацию
- Сообщения о смене команды локализуются для каждого игрока отдельно
- HTML-центр работает только для живых игроков (если `ShowHtmlWhenDead: false`)
- Анонсы серверов по умолчанию отключены (`Enabled: false`)

### Совместимость

- CounterStrikeSharp **>= 1.0.369** (`MinimumApiVersion` 369) — или SwiftlyS2 **>= 1.4.9**
  (`MinimumAPIVersion` 1.4.9); .NET 10 в обоих случаях
- Windows и Linux

### Почему не меню SwiftlyS2 для панели в центре

В SwiftlyS2 есть встроенная система экранных меню. Для вывода она сознательно не используется:
меню — эксклюзивная интерактивная поверхность, одно активное меню на игрока, и реклама закрыла бы
меню, открытое другим плагином, а в серверном режиме ввода `wasd` ещё и перехватила бы клавиши
движения. Под капотом оно рисуется той же HTML-панелью центра экрана, что и наш канал
`CenterHtml`, — так что внешний вид (заголовок, размеры, цвета) доступен и без этих побочных
эффектов.

### Безопасность

- `css_servers` / `sw_servers` доступны любому игроку, поэтому ограничены кулдауном на игрока
  и никогда не блокируют главный поток
- A2S-ответы принимаются только с опрашиваемого адреса, каждое чтение проверяет границы буфера
- Текст, пришедший от чужого сервера (имена карт), санируется перед подстановкой в шаблон:
  фигурные скобки, управляющие символы и переносы строк убираются, длина ограничена — чужой
  админ не может вставить в ваш чат цветовые теги или многострочный спам
- Ники игроков, подставляемые в HTML-панель, экранируются
- `Debug` выключен по умолчанию: он пишет в лог SteamID, ники и гео игроков

---

## 📄 Лицензия

Copyright (C) 2025-2026 Armatura

Это свободное программное обеспечение: вы можете распространять и/или изменять его на условиях
GNU General Public License, опубликованной Free Software Foundation, версии 3 или (на ваш выбор)
любой более поздней версии.

Программа распространяется в надежде, что она будет полезной, но **без каких-либо гарантий**;
даже без подразумеваемой гарантии товарного состояния или пригодности для конкретной цели.
Подробности — в [GNU General Public License](LICENSE).

### Сторонние компоненты

| Компонент | Лицензия |
|-----------|----------|
| [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) | MIT |
| [MaxMind.GeoIP2](https://github.com/maxmind/GeoIP2-dotnet) | Apache-2.0 |
| Базы GeoLite2 (`GeoIP/*.mmdb`) | [MaxMind GeoLite2 EULA](https://www.maxmind.com/en/geolite2/eula) — **не** покрываются GPL этого проекта |

Базы GeoLite2, лежащие в `GeoIP/` и попадающие в релизные архивы, остаются под условиями MaxMind.
Продукт содержит данные GeoLite2, созданные MaxMind, доступные на
[maxmind.com](https://www.maxmind.com).

---

## 💬 Поддержка

Если у вас возникли вопросы или проблемы:
1. Проверьте логи сервера (включите `Debug: true`)
2. Убедитесь, что файлы GeoIP на месте
3. Проверьте права на команды (`@css/root` в CSSharp, `notifymessages.admin` в SwiftlyS2)
4. Создайте [Issue](https://github.com/Armatura-Create/NotifyMessagesCS2/issues) с подробным описанием

Репозиторий: https://github.com/Armatura-Create/NotifyMessagesCS2
