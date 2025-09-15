# NotifyMessages (CS2)

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![CounterStrikeSharp](https://img.shields.io/badge/CounterStrikeSharp-API-1f6feb?logo=steam)](https://github.com/roflmuffin/CounterStrikeSharp)
[![Platforms](https://img.shields.io/badge/Platforms-Linux%20%7C%20Windows-2ea44f)](#)
[![Release](https://img.shields.io/badge/Release-ZIP%20package-success)](#build-и-упаковка)
[![GeoLite2](https://img.shields.io/badge/GeoLite2-Auto--download-009688)](#geolite2-данные-автозагрузка-при-сборке)

Универсальный плагин для CounterStrikeSharp/CS2 для уведомлений и рекламы: сообщения в чат, центр экрана (в т.ч. HTML), консоль, а также анонсы других серверов.


## Установка
1. Установите CounterStrikeSharp и Metamod:Source.
2. Скачайте собранный архив NotifyMessages.zip из релизов или соберите проект сами.
3. Распакуйте содержимое архива в корень игрового сервера. В архиве уже готова структура:
   addons/counterstrikesharp/plugins/NotifyMessages
   и файлы: NotifyMessages.dll, NotifyMessages.pdb, NotifyMessages.deps.json, MaxMind.GeoIP2.dll, MaxMind.Db.dll, GeoLite2-Country.mmdb, GeoLite2-City.mmdb.


## Конфигурация (split-файлы)
Плагин автоматически создаёт и использует разделённые конфиги в папке configs/plugins/NotifyMessages рядом с dll:
- Settings.json — базовые настройки, локализация, приветствия, карты.
- Ads.json — блоки рекламы (интервалы и наборы сообщений Chat/Center/Console).
- Messages.json — шаблоны сообщений входа/выхода (по языкам).
- Servers.json — анонсы серверов (заголовок, включение, интервал опроса, список адресов).

Все файлы генерируются при первом запуске с наполненными значениями по умолчанию. Их можно править на лету и затем выполнить команду css_advert_reload.

### Settings.json
Основные поля:
- Debug: bool — подробный лог в консоль сервера.
- PrintToCenterHtml: bool — выводить центр через HTML.
- HtmlCenterDuration: number — длительность HTML-центра (сек), если включен.
- ShowHtmlWhenDead: bool — показывать HTML-центр в наблюдателях.
- WelcomeMessage: { MessageType, Message, DisplayDelay } — персональное приветствие.
- ChangeTeamMessage, JoinTeamMessage — шаблоны для смены/вступления в команду.
- RestartMessage, UpdateMessage — сообщения для анонсов рестарта/обновления.
- DefaultLang: "RU" | "US" | … — язык по умолчанию.
- LanguageMessages: { тег: { ISO: "строка" } } — словарь переводов для {prefix}, {welcome_player}, и др.
- MapsName: { "de_mirage": "MIRAGE_CLASSIC", … } — переименования карт.

Минимальный пример:
```json
{
  "Debug": false,
  "PrintToCenterHtml": false,
  "WelcomeMessage": {
    "MessageType": 0,
    "Message": "{prefix}{welcome_player} {RED}{PLAYERNAME} {DEFAULT}{welcome_text}",
    "DisplayDelay": 5
  },
  "DefaultLang": "RU"
}
```

### Ads.json
- Ads: массив блоков рекламы. Каждый блок:
  - Interval: число (сек)
  - Messages: массив объектов с ключами Chat, Center и/или Console

Пример блока:
```json
{
  "Ads": [
    {
      "Interval": 60,
      "Messages": [
        { "Chat": "{prefix}{reklama_1}" },
        { "Chat": "{prefix}{reklama_2}", "Center": "!viptest - FREE!" }
      ]
    }
  ]
}
```

### Messages.json
- JoinMessages: { ISO: ["…", "…"] }
- LeaveMessages: { ISO: ["…", "…"] }

Сообщения поддерживают цветовые теги ({RED}, {GREEN}, {DEFAULT}, …) и теги данных: {PLAYERNAME}, {COUNTRY}, {CITY}. Язык берётся из GeoIP по игроку (или DefaultLang).

### Servers.json
- TitleAnnounceServers: строка-заголовок (может быть локализуемым тегом, например {announce_servers}).
- Servers:
  - Enabled: bool — включить анонс серверов (по умолчанию false).
  - Interval: число — период опроса статусов.
  - QueryTimeoutMs: число (реком. 200–5000) — таймаут запроса.
  - CacheTtlSeconds: число (0–60) — TTL кеша ответов.
  - List: массив серверов { Ip, Port, MessageTemplate, MessageTemplateConsole, MaxPlayersFallback }.

Минимальный пример:
```json
{
  "TitleAnnounceServers": "{announce_servers}",
  "Servers": {
    "Enabled": false,
    "Interval": 125,
    "QueryTimeoutMs": 1000,
    "CacheTtlSeconds": 5,
    "List": [
      {
        "Ip": "127.0.0.1",
        "Port": 27015,
        "MessageTemplate": "{LIGHTBLUE}➡{DEFAULT} {GREEN}{SERVER_IP}:{SERVER_PORT}{DEFAULT} - {LIGHTBLUE}{SERVER_MAP}{DEFAULT} | {GREEN}{SERVER_PLAYERS}{DEFAULT}/{GREEN}{SERVER_MAXPLAYERS}",
        "MessageTemplateConsole": "{SERVER_IP}:{SERVER_PORT} - {SERVER_MAP} | {SERVER_PLAYERS}/{SERVER_MAXPLAYERS}",
        "MaxPlayersFallback": 24
      }
    ]
  }
}
```


## Доступные команды
- css_servers — показать список серверов (если включены анонсы).
- css_announce_restart <сек> — объявить рестарт через N секунд.
- css_announce_update <сек> — объявить обновление через N секунд.
- css_advert_reload — перезагрузить конфигурации (требует права @css/root).


## Build и упаковка
- Сборка релиза: dotnet build -c Release
- Готовый архив: bin/Release/net8.0/NotifyMessages.zip
- Внутри архива уже нужная структура addons/counterstrikesharp/plugins/NotifyMessages/ с файлами плагина и зависимостями. Архив можно просто распаковать в корень сервера.


## GeoLite2 данные (автозагрузка при сборке)
Чтобы в релиз попадали актуальные базы GeoLite2-Country.mmdb и GeoLite2-City.mmdb:

Способ 1 (переменная окружения, рекомендовано для CI):
- macOS/Linux:
  - export MAXMIND_LICENSE_KEY=ВАШ_КЛЮЧ
  - dotnet build -c Release
- Windows (PowerShell):
  - setx MAXMIND_LICENSE_KEY "ВАШ_КЛЮЧ"
  - Перезапустите терминал/IDE и соберите: dotnet build -c Release

Способ 2 (свойство MSBuild):
- dotnet build -c Release -p:GeoLiteLicenseKey=ВАШ_КЛЮЧ

Способ 3 (локальный props-файл для разработчиков):
- Скопируйте Directory.Build.props.example в Directory.Build.props и укажите ключ. Файл исключён из git.

Если ключ не задан или загрузка не удалась, сборка покажет сообщение о пропуске и использует локальный фолбэк: положите GeoLite2-Country.mmdb и GeoLite2-City.mmdb в папку GeoIP/ в корне репозитория — они будут автоматически скопированы в выходную папку и включены в релизный ZIP.


## Примечания
- Цветовые теги автоматически конвертируются в управляющие коды чата CS2. Доступны: {DEFAULT}/{WHITE}, {GREEN}, {RED}, {LIGHTBLUE}, {YELLOW}/{GOLD}, {SILVER}, {BLUE}, {DARKBLUE}, {PURPLE}/{LIGHTPURPLE}, {GREY}, {ORANGE}, {MAGENTA} и др. Также поддерживается {SPACE} и переносы строк \n.
- Имена карт в сообщениях могут быть заменены по словарю MapsName.
- Анонсы серверов по умолчанию отключены (Enabled=false), но в конфиге создаётся понятная заготовка.
- Опционально: если установлен IksAdmin и его API, команда hide может инициировать «фейковое» сообщение о выходе игрока.


— Автор: Armatura  •  Версия: v2.0.0
