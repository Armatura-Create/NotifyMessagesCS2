# NotifyMessages (CS2)

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![CounterStrikeSharp](https://img.shields.io/badge/CounterStrikeSharp-API-1f6feb?logo=steam)](https://github.com/roflmuffin/CounterStrikeSharp)
[![Platforms](https://img.shields.io/badge/Platforms-Linux%20%7C%20Windows-2ea44f)](#)
[![Release](https://img.shields.io/badge/Release-ZIP%20package-success)](#build-и-упаковка)
[![GeoLite2](https://img.shields.io/badge/GeoLite2-Auto--download-009688)](#geolite2-данные-автозагрузка-при-сборке)

Универсальный плагин для CounterStrikeSharp/CS2 для уведомлений и рекламы: сообщения в чат, центр экрана (в т.ч. HTML), консоль, а также анонсы других серверов через A2S-запросы.

## ✨ Особенности

- 🌍 **Мультиязычность** — автоматическое определение языка по GeoIP (5+ языков)
- 🎨 **Цветные сообщения** — поддержка 15+ цветовых тегов
- 📱 **Множество каналов вывода** — чат, центр экрана, HTML-центр, консоль
- 🔄 **Модульная конфигурация** — 4 отдельных конфига для удобства
- 🖥️ **Мониторинг серверов** — A2S-запросы с асинхронным кешированием
- ⚡ **Высокая производительность** — кеширование сообщений, оптимизированные регулярные выражения
- 🔒 **Thread-safe** — безопасная работа в многопоточной среде
- 🚀 **Фоновые операции** — все запросы к серверам выполняются асинхронно, без блокировки UI

## 📦 Установка

1. Установите [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) и Metamod:Source
2. Скачайте собранный архив `NotifyMessages.zip` из релизов или соберите проект сами
3. Распакуйте содержимое архива в корень игрового сервера

В архиве уже готова структура:
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

4. Запустите сервер — плагин автоматически создаст конфигурационные файлы

## ⚙️ Конфигурация

Плагин использует **модульную систему конфигурации** — 4 отдельных JSON-файла в папке:
```
csgo/addons/counterstrikesharp/configs/plugins/NotifyMessages/
├── Settings.json    # Основные настройки и локализация
├── Ads.json         # Рекламные сообщения
├── Messages.json    # Сообщения входа/выхода игроков
└── Servers.json     # Мониторинг других серверов
```

Все файлы создаются автоматически при первом запуске. После редактирования используйте команду `css_advert_reload` для применения изменений без перезапуска сервера.

---

### 📄 Settings.json — Основные настройки

**Назначение:** Базовые параметры плагина, локализация, приветственные сообщения, замена названий карт.

#### Структура:

```json
{
  "Debug": false,
  "PrintToCenterHtml": false,
  "HtmlCenterDuration": 5.0,
  "ShowHtmlWhenDead": false,
  "DefaultLang": "RU",
  
  "WelcomeMessage": {
    "MessageType": 0,
    "Message": "{prefix}{welcome_player} {RED}{PLAYERNAME} {DEFAULT}{welcome_text}",
    "DisplayDelay": 5
  },
  
  "ChangeTeamMessage": "{prefix}{changeTeamMessage}",
  "JoinTeamMessage": "{prefix}{joinTeamMessage}",
  "RestartMessage": "{prefix}{RED}{will_restarted}",
  "UpdateMessage": "{prefix}{RED}{will_updated}",
  
  "LanguageMessages": {
    "prefix": {
      "RU": "{LIGHTBLUE}Armaturix ➡{DEFAULT} ",
      "US": "{LIGHTBLUE}Armaturix ➡{DEFAULT} ",
      "UA": "{LIGHTBLUE}Armaturix ➡{DEFAULT} ",
      "PL": "{LIGHTBLUE}Armaturix ➡{DEFAULT} ",
      "DE": "{LIGHTBLUE}Armaturix ➡{DEFAULT} "
    },
    "welcome_player": {
      "RU": "Добро пожаловать",
      "US": "Welcome",
      "UA": "Ласкаво просимо",
      "PL": "Witamy",
      "DE": "Willkommen"
    },
    "welcome_text": {
      "RU": "на игровой сервер {RED}Armaturix",
      "US": "to the game server {RED}Armaturix",
      "UA": "на ігровий сервер {RED}Armaturix",
      "PL": "na serwer gry {RED}Armaturix",
      "DE": "auf den Spieleserver {RED}Armaturix"
    },
    "will_restarted": {
      "RU": "Сервер будет перезапущен через {TIME_RESTART}!",
      "US": "The server will be restarted in {TIME_RESTART}!",
      "UA": "Сервер буде перезапущено через {TIME_RESTART}!",
      "PL": "Serwer zostanie ponownie uruchomiony za {TIME_RESTART}!",
      "DE": "Der Server wird in {TIME_RESTART} neu gestartet!"
    },
    "will_updated": {
      "RU": "Вышло обновление! Сервер будет перезапущен через {TIME_RESTART}.",
      "US": "An update has been released! The server will restart in {TIME_RESTART}.",
      "UA": "Вийшло оновлення! Сервер буде перезапущено через {TIME_RESTART}.",
      "PL": "Wydano aktualizację! Serwer zostanie ponownie uruchomiony za {TIME_RESTART}.",
      "DE": "Ein Update wurde veröffentlicht! Der Server wird in {TIME_RESTART} neu gestartet."
    },
    "changeTeamMessage": {
      "RU": "{GREEN}{PLAYERNAME}{DEFAULT} перешел из команды {OLD_TEAM} в команду {TEAM}",
      "US": "{GREEN}{PLAYERNAME}{DEFAULT} switched from {OLD_TEAM} to {TEAM}",
      "UA": "{GREEN}{PLAYERNAME}{DEFAULT} перейшов з команди {OLD_TEAM} до команди {TEAM}",
      "PL": "{GREEN}{PLAYERNAME}{DEFAULT} przeszedł z drużyny {OLD_TEAM} do drużyny {TEAM}",
      "DE": "{GREEN}{PLAYERNAME}{DEFAULT} wechselte von {OLD_TEAM} zu {TEAM}"
    },
    "joinTeamMessage": {
      "RU": "{GREEN}{PLAYERNAME}{DEFAULT} присоединился к {TEAM}",
      "US": "{GREEN}{PLAYERNAME}{DEFAULT} joined {TEAM}",
      "UA": "{GREEN}{PLAYERNAME}{DEFAULT} приєднався до {TEAM}",
      "PL": "{GREEN}{PLAYERNAME}{DEFAULT} dołączył do {TEAM}",
      "DE": "{GREEN}{PLAYERNAME}{DEFAULT} trat {TEAM} bei"
    }
  },
  
  "MapsName": {
    "de_mirage": "MIRAGE",
    "de_dust2": "DUST 2",
    "de_inferno": "INFERNO",
    "de_nuke": "NUKE",
    "de_overpass": "OVERPASS",
    "de_vertigo": "VERTIGO",
    "de_ancient": "ANCIENT",
    "de_anubis": "ANUBIS"
  }
}
```

#### Параметры:

| Параметр | Тип | Описание |
|----------|-----|----------|
| `Debug` | bool | Включить подробное логирование в консоль сервера |
| `PrintToCenterHtml` | bool | Использовать HTML-режим для центральных сообщений |
| `HtmlCenterDuration` | float | Длительность показа HTML-сообщения в секундах |
| `ShowHtmlWhenDead` | bool | Показывать HTML-центр мёртвым игрокам/наблюдателям |
| `DefaultLang` | string | Язык по умолчанию (RU/US/UA/PL/DE) |
| `WelcomeMessage` | object | Приветственное сообщение при подключении |
| `ChangeTeamMessage` | string | Шаблон сообщения при смене команды |
| `JoinTeamMessage` | string | Шаблон при первом вступлении в команду |
| `RestartMessage` | string | Сообщение о рестарте сервера |
| `UpdateMessage` | string | Сообщение об обновлении |
| `LanguageMessages` | object | Словарь переводов для всех языков |
| `MapsName` | object | Замена технических названий карт на красивые |

#### WelcomeMessage:

```json
{
  "MessageType": 0,      // 0=Chat, 1=Center, 2=CenterHtml, 3=Console
  "Message": "...",      // Текст с поддержкой всех плейсхолдеров
  "DisplayDelay": 5      // Задержка в секундах перед показом
}
```

---

### 📢 Ads.json — Рекламные сообщения

**Назначение:** Циклические рекламные сообщения с настраиваемыми интервалами.

#### Полный пример:

```json
{
  "Ads": [
    {
      "Interval": 60,
      "Messages": [
        { 
          "Chat": "{prefix}{reklama_1}" 
        },
        { 
          "Chat": "{prefix}{reklama_2}", 
          "Center": "!viptest - FREE!" 
        },
        { 
          "Chat": "{prefix}{reklama_3}", 
          "Console": "Discord: discord.gg/example" 
        }
      ]
    },
    {
      "Interval": 120,
      "Messages": [
        { 
          "Chat": "{prefix}{reklama_shop}" 
        }
      ]
    }
  ]
}
```

#### Структура рекламного блока:

| Параметр | Описание |
|----------|----------|
| `Interval` | Интервал показа в секундах (минимум 1) |
| `Messages` | Массив сообщений (показываются по очереди) |

#### Каналы вывода:

- `"Chat"` — в чат
- `"Center"` — в центр экрана (обычный)
- `"Console"` — в консоль игрока

Можно комбинировать несколько каналов в одном сообщении!

#### Примеры тегов локализации в Settings.json:

```json
"LanguageMessages": {
  "reklama_1": {
    "RU": "Хочешь крутые скины? Используй команды:\n➡ !ws\n➡ !knife\n➡ !gloves",
    "US": "Want awesome skins? Use commands:\n➡ !ws\n➡ !knife\n➡ !gloves"
  },
  "reklama_2": {
    "RU": "Хочешь попробовать VIP? Активируй бесплатно на час:\n{RED}➡ !viptest",
    "US": "Want to try VIP? Activate for free for 1 hour:\n{RED}➡ !viptest"
  },
  "reklama_3": {
    "RU": "Общайся в нашем Discord: {RED}discord.gg/example",
    "US": "Join our Discord: {RED}discord.gg/example"
  }
}
```

---

### 👥 Messages.json — Сообщения входа/выхода

**Назначение:** Сообщения о подключении и отключении игроков с автоопределением страны и города.

#### Полный пример:

```json
{
  "JoinMessages": {
    "RU": [
      "{connected} Игрок {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] вылез из {GREEN}{COUNTRY}{DEFAULT}! Салют!",
      "{connected} Игрок {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] ворвался из {GREEN}{COUNTRY}{DEFAULT}, как царь!",
      "{connected} Игрок {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] прибыл из {GREEN}{CITY}, {COUNTRY}{DEFAULT}!"
    ],
    "US": [
      "{connected} Player {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] emerged from {GREEN}{COUNTRY}{DEFAULT}!",
      "{connected} Player {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] rushed in from {GREEN}{COUNTRY}{DEFAULT}!",
      "{connected} Player {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] arrived from {GREEN}{CITY}, {COUNTRY}{DEFAULT}!"
    ],
    "UA": [
      "{connected} Гравець {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] виліз із {GREEN}{COUNTRY}{DEFAULT}!",
      "{connected} Гравець {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] прибув із {GREEN}{CITY}, {COUNTRY}{DEFAULT}!"
    ],
    "PL": [
      "{connected} Gracz {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] wynurzył się z {GREEN}{COUNTRY}{DEFAULT}!",
      "{connected} Gracz {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] przybył z {GREEN}{CITY}, {COUNTRY}{DEFAULT}!"
    ],
    "DE": [
      "{connected} Spieler {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] kam aus {GREEN}{COUNTRY}{DEFAULT}!",
      "{connected} Spieler {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] ist aus {GREEN}{CITY}, {COUNTRY}{DEFAULT} angekommen!"
    ]
  },
  
  "LeaveMessages": {
    "RU": [
      "{disconnected} {DARKRED}Игрок {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}превратился в пиксели{GREY}…",
      "{disconnected} {DARKRED}Игрок {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}выбыл из игры!",
      "{disconnected} {DARKRED}Игрок {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}вышел из матрицы."
    ],
    "US": [
      "{disconnected} {DARKRED}Player {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}turned into pixels{GREY}…",
      "{disconnected} {DARKRED}Player {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}left the game!",
      "{disconnected} {DARKRED}Player {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}exited the matrix."
    ],
    "UA": [
      "{disconnected} {DARKRED}Гравець {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}перетворився на пікселі{GREY}…",
      "{disconnected} {DARKRED}Гравець {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}вийшов з гри!"
    ],
    "PL": [
      "{disconnected} {DARKRED}Gracz {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}zmienił się w piksele{GREY}…",
      "{disconnected} {DARKRED}Gracz {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}opuścił grę!"
    ],
    "DE": [
      "{disconnected} {DARKRED}Spieler {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}wurde zu Pixeln{GREY}…",
      "{disconnected} {DARKRED}Spieler {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}hat das Spiel verlassen!"
    ]
  }
}
```

#### Доступные плейсхолдеры:

| Плейсхолдер | Описание |
|-------------|----------|
| `{PLAYERNAME}` | Имя игрока |
| `{COUNTRY}` | Страна (определяется по IP через GeoIP) |
| `{CITY}` | Город (определяется по IP через GeoIP) |
| `{connected}` | Локализованный префикс "Подключился" |
| `{disconnected}` | Локализованный префикс "Отключился" |

**Важно:** Плагин выбирает случайное сообщение из массива для разнообразия!

---

### 🖥️ Servers.json — Мониторинг серверов

**Назначение:** Отображение статуса других серверов через A2S-протокол с асинхронным кешированием.

#### Полный пример:

```json
{
  "TitleAnnounceServers": "{announce_servers}",
  
  "Servers": {
    "Enabled": true,
    "Interval": 125,
    "QueryTimeoutMs": 1000,
    "CacheTtlSeconds": 10,
    
    "List": [
      {
        "Ip": "192.168.1.100",
        "Port": 27015,
        "MessageTemplate": "{LIGHTBLUE}➡{DEFAULT} {GREEN}{SERVER_IP}:{SERVER_PORT}{DEFAULT} - {LIGHTBLUE}{SERVER_MAP}{DEFAULT} | {GREEN}{SERVER_PLAYERS}{DEFAULT}/{GREEN}{SERVER_MAXPLAYERS}",
        "MessageTemplateConsole": "{SERVER_IP}:{SERVER_PORT} - {SERVER_MAP} | {SERVER_PLAYERS}/{SERVER_MAXPLAYERS}",
        "MaxPlayersFallback": 24
      },
      {
        "Ip": "play.example.com",
        "Port": 27016,
        "MessageTemplate": "{LIGHTBLUE}➡{DEFAULT} {RED}VIP Server{DEFAULT} - {LIGHTBLUE}{SERVER_MAP}{DEFAULT} | {GREEN}{SERVER_PLAYERS}{DEFAULT}/{GREEN}{SERVER_MAXPLAYERS}",
        "MessageTemplateConsole": "VIP Server - {SERVER_MAP} | {SERVER_PLAYERS}/{SERVER_MAXPLAYERS}",
        "MaxPlayersFallback": 16
      },
      {
        "Ip": "127.0.0.1",
        "Port": 27017,
        "MessageTemplate": "{LIGHTBLUE}➡{DEFAULT} {YELLOW}AWP Only{DEFAULT} - {LIGHTBLUE}{SERVER_MAP}{DEFAULT} | {GREEN}{SERVER_PLAYERS}{DEFAULT}/{GREEN}{SERVER_MAXPLAYERS}",
        "MessageTemplateConsole": "AWP Only - {SERVER_MAP} | {SERVER_PLAYERS}/{SERVER_MAXPLAYERS}",
        "MaxPlayersFallback": 32
      }
    ]
  }
}
```

#### Параметры:

| Параметр | Тип | Описание |
|----------|-----|----------|
| `Enabled` | bool | Включить/выключить мониторинг серверов |
| `Interval` | float | Интервал автоматического опроса в секундах (минимум 5) |
| `QueryTimeoutMs` | int | Таймаут A2S-запроса в миллисекундах (200-5000) |
| `CacheTtlSeconds` | int | Время жизни кеша в секундах (0-60, 0=без кеша) |

#### Параметры сервера:

| Параметр | Описание |
|----------|----------|
| `Ip` | IP-адрес или hostname сервера |
| `Port` | Порт сервера |
| `MessageTemplate` | Шаблон для чата (с цветами) |
| `MessageTemplateConsole` | Шаблон для консоли (без цветов) |
| `MaxPlayersFallback` | Макс. игроков если сервер оффлайн |

#### Плейсхолдеры для шаблонов:

| Плейсхолдер | Описание |
|-------------|----------|
| `{SERVER_IP}` | IP-адрес сервера |
| `{SERVER_PORT}` | Порт сервера |
| `{SERVER_MAP}` | Текущая карта (или "OFFLINE") |
| `{SERVER_PLAYERS}` | Количество игроков |
| `{SERVER_MAXPLAYERS}` | Максимум игроков |

#### Особенности работы:

- **Асинхронные запросы** — не блокируют сервер при загрузке
- **Фоновое обновление** — после показа списка кеш обновляется в фоне
- **Кеширование** — повторные запросы берутся из кеша
- **Thread-safe** — безопасная работа в многопоточной среде

---

## 🎨 Цветовые теги

Плагин поддерживает следующие цветовые теги (автоматически конвертируются в коды CS2):

| Тег | Цвет | Тег | Цвет |
|-----|------|-----|------|
| `{DEFAULT}` / `{WHITE}` | Белый | `{RED}` | Красный |
| `{GREEN}` / `{LIME}` | Зелёный | `{BLUE}` | Синий |
| `{YELLOW}` / `{GOLD}` | Жёлтый | `{ORANGE}` | Оранжевый |
| `{LIGHTBLUE}` | Голубой | `{DARKBLUE}` | Тёмно-синий |
| `{PURPLE}` / `{LIGHTPURPLE}` | Фиолетовый | `{MAGENTA}` | Пурпурный |
| `{GREY}` / `{GRAY}` | Серый | `{SILVER}` | Серебряный |
| `{DARKRED}` | Тёмно-красный | `{OLIVE}` | Оливковый |
| `{LIGHTYELLOW}` | Светло-жёлтый | `{BLUEGREY}` | Сине-серый |

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

### Для игроков:

| Команда | Описание |
|---------|----------|
| `css_servers` | Показать список серверов из кеша |

**Особенность:** После показа списка запускается фоновое обновление кеша, чтобы следующий запрос показал актуальные данные.

### Для администраторов:

| Команда | Права | Описание |
|---------|-------|----------|
| `css_announce_restart <сек>` | SERVER_ONLY | Объявить рестарт через N секунд (1-3600) |
| `css_announce_update <сек>` | SERVER_ONLY | Объявить обновление через N секунд (1-3600) |
| `css_advert_reload` | @css/root | Перезагрузить все конфигурации |

**Важно:** Команды `css_announce_restart` и `css_announce_update` имеют ограничение **от 1 до 3600 секунд** (1 час) для безопасности.

#### Примеры:

```
css_announce_restart 300     // Рестарт через 5 минут
css_announce_update 60       // Обновление через 1 минуту
css_advert_reload            // Перезагрузить конфиги
```

---

## ⚡ Оптимизации и производительность

### Реализованные улучшения:

1. **Кеширование сообщений по языку**
   - Сообщения обрабатываются один раз для каждого языка
   - Если у всех игроков RU язык, обработка происходит только один раз

2. **Скомпилированные регулярные выражения**
   - Regex для парсинга тегов компилируется один раз при загрузке
   - Значительное ускорение обработки сообщений

3. **Асинхронные A2S-запросы**
   - Все запросы к другим серверам выполняются в фоне
   - Не блокируют загрузку плагина и работу игрового сервера
   - Используется `Task.Run` и `async/await`

4. **Thread-safe операции**
   - Все критические секции защищены `lock`
   - SessionService безопасен для многопоточного доступа
   - ServerStatusService использует потокобезопасный кеш

5. **Улучшенное логирование**
   - Временные метки для всех сообщений
   - Полные Stack Trace для ошибок
   - Структурированный формат: `[timestamp] [plugin] [level] message`

---

## 🔧 Build и упаковка

### Сборка релиза:

```bash
dotnet build -c Release
```

Готовый архив: `bin/Release/net8.0/NotifyMessages.zip`

Внутри архива уже правильная структура `addons/counterstrikesharp/plugins/NotifyMessages/` с файлами плагина и зависимостями. Архив можно просто распаковать в корень сервера.

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

- Плагин автоматически определяет страну игрока по IP через GeoIP2
- Поддерживаются языки: RU, US, UA, PL, DE
- Можно легко добавить новые языки в `LanguageMessages`
- Если язык игрока не найден, используется `DefaultLang`

### Особенности работы

- Приветственные сообщения поддерживают все плейсхолдеры и локализацию
- Сообщения о смене команды локализуются для каждого игрока отдельно
- HTML-центр работает только для живых игроков (если `ShowHtmlWhenDead: false`)
- Анонсы серверов по умолчанию отключены (`Enabled: false`)

### Совместимость

- Минимальная версия CounterStrikeSharp API: 339
- .NET 8.0
- Windows и Linux

### Безопасность

- Все входные данные команд валидируются
- Ограничения на время рестарта/обновления (1-3600 сек)
- Thread-safe операции во всех критических секциях
- Защита от переполнения при A2S-запросах

---

## 🐛 Исправленные баги (v2.0.0)

- ✅ Исправлен расчёт времени HTML-центра (`.Seconds` → `.TotalSeconds`)
- ✅ Удалено дублирование `InitialQuery()` в команде reload
- ✅ Добавлена потокобезопасность в `SessionService`
- ✅ Добавлены ограничения на команды announce (1-3600 сек)
- ✅ Оптимизированы регулярные выражения (скомпилированные)
- ✅ Реализовано кеширование обработанных сообщений
- ✅ Улучшена система логирования (временные метки, stack trace)
- ✅ A2S-запросы теперь полностью асинхронны (не блокируют UI)

---

## 📖 Changelog

### v2.0.0 (2024-11-26)

#### 🚀 Новое
- Кеширование обработанных сообщений по языку
- Асинхронные фоновые A2S-запросы
- Фоновое обновление кеша серверов после команды `css_servers`
- Улучшенное логирование с временными метками

#### ⚡ Оптимизации
- Скомпилированные регулярные выражения
- Thread-safe операции во всех сервисах
- Валидация команд (лимиты 1-3600 секунд)

#### 🐛 Исправления
- Корректный расчёт времени HTML-центра
- Удалено дублирование InitialQuery()
- Исправлена локализация в WelcomeMessage
- Исправлена локализация сообщений о смене команды

---

**Автор:** Armatura  
**Версия:** v2.0.0  
**Лицензия:** MIT  
**GitHub:** [ваша-ссылка-здесь]

---

## 💬 Поддержка

Если у вас возникли вопросы или проблемы:
1. Проверьте логи сервера (включите `Debug: true`)
2. Убедитесь, что файлы GeoIP на месте
3. Проверьте права на команды (@css/root для reload)
4. Создайте Issue на GitHub с подробным описанием
