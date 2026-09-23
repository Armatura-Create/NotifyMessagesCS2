// Тексты файлов конфигурации по умолчанию.
//
// Четыре конфига и четыре схемы — байт в байт то, что пишет C#-цель при первом
// запуске (ConfigService.Defaults.cs / ConfigService.Schemas.cs, сериализация
// System.Text.Json). Не форматировать руками: дефолты — часть контракта, и
// правятся в cssharp/, swiftly/ и здесь одновременно. Отличаются только имена
// команд: у этой цели префикс mm_.
//
// Каждый литерал короче 16 КБ: это предел одной строковой константы в MSVC.
#include "core/config.h"

namespace nm {
namespace defaults {

const char* SettingsJson() {
    return R"NM({
  "$schema": "./Settings.schema.json",
  "Debug": false,
  "DefaultLang": "RU",
  "PrintToCenterHtml": false,
  "WelcomeMessage": {
    "MessageType": "Chat",
    "Message": "{prefix}{welcome_player} {RED}{PLAYERNAME} {DEFAULT}{welcome_text}",
    "DisplayDelay": 5
  },
  "ChangeTeamMessage": "{prefix}{changeTeamMessage}",
  "JoinTeamMessage": "{prefix}{joinTeamMessage}",
  "TitleAnnounceServers": "{prefix}{announce_servers}",
  "RestartNotify": {
    "Enabled": true,
    "MessageType": "Chat",
    "DefaultMessage": "{prefix}{RED}{restart_in_seconds}",
    "Thresholds": {
      "300": "{prefix}{RED}{update_available} {DEFAULT}{restart_in_5min}",
      "60": "{prefix}{RED}{update_available} {DEFAULT}{restart_in_1min}",
      "30": "{prefix}{RED}{restart_in_30sec}",
      "10": "{prefix}{RED}{restart_in_10sec}",
      "5": "{prefix}{RED}{restart_in_seconds}",
      "4": "{prefix}{RED}{restart_in_seconds}",
      "3": "{prefix}{RED}{restart_in_seconds}",
      "2": "{prefix}{RED}{restart_in_seconds}",
      "1": "{prefix}{RED}{restart_now}"
    }
  },
  "MapsName": {
    "de_dust2": "Dust 2",
    "de_mirage": "Mirage",
    "de_inferno": "Inferno",
    "de_nuke": "Nuke",
    "de_overpass": "Overpass",
    "de_vertigo": "Vertigo",
    "de_ancient": "Ancient",
    "de_anubis": "Anubis"
  },
  "LanguageAliases": {
    "RU": [
      "ru",
      "be",
      "kk",
      "BY",
      "KZ",
      "MD",
      "AM",
      "KG",
      "UZ"
    ],
    "US": [
      "en",
      "GB",
      "CA",
      "AU",
      "NZ",
      "IE"
    ],
    "UA": [
      "uk"
    ],
    "PL": [
      "pl"
    ],
    "DE": [
      "de",
      "AT",
      "CH"
    ]
  }
})NM";
}

const char* MessagesJson() {
    return R"NM({
  "$schema": "./Messages.schema.json",
  "LanguageMessages": {
    "prefix": {
      "RU": "{LIGHTBLUE}Server ➡{DEFAULT} ",
      "US": "{LIGHTBLUE}Server ➡{DEFAULT} ",
      "UA": "{LIGHTBLUE}Server ➡{DEFAULT} ",
      "PL": "{LIGHTBLUE}Server ➡{DEFAULT} ",
      "DE": "{LIGHTBLUE}Server ➡{DEFAULT} "
    },
    "changeTeamMessage": {
      "RU": "{GREEN}{PLAYERNAME}{DEFAULT} перешел из команды {BLUE}{OLD_TEAM} в команду {BLUE}{TEAM}",
      "US": "{GREEN}{PLAYERNAME}{DEFAULT} switched from {BLUE}{OLD_TEAM} to {BLUE}{TEAM}",
      "UA": "{GREEN}{PLAYERNAME}{DEFAULT} перейшов з команди {BLUE}{OLD_TEAM} до команди {BLUE}{TEAM}",
      "PL": "{GREEN}{PLAYERNAME}{DEFAULT} przeszedł z drużyny {BLUE}{OLD_TEAM} do drużyny {BLUE}{TEAM}",
      "DE": "{GREEN}{PLAYERNAME}{DEFAULT} wechselte von {BLUE}{OLD_TEAM} zu {BLUE}{TEAM}"
    },
    "joinTeamMessage": {
      "RU": "{GREEN}{PLAYERNAME}{DEFAULT} присоединился к {TEAM}",
      "US": "{GREEN}{PLAYERNAME}{DEFAULT} joined {TEAM}",
      "UA": "{GREEN}{PLAYERNAME}{DEFAULT} приєднався до {TEAM}",
      "PL": "{GREEN}{PLAYERNAME}{DEFAULT} dołączył do {TEAM}",
      "DE": "{GREEN}{PLAYERNAME}{DEFAULT} trat {TEAM} bei"
    },
    "update_available": {
      "RU": "Вышло обновление CS2!",
      "US": "A new CS2 update is available!",
      "UA": "Вийшло оновлення CS2!",
      "PL": "Dostepna jest nowa aktualizacja CS2!",
      "DE": "Ein neues CS2-Update ist verfugbar!"
    },
    "restart_in_5min": {
      "RU": "Сервер перезапустится через 5 минут.",
      "US": "The server will restart in 5 minutes.",
      "UA": "Сервер перезапуститься через 5 хвилин.",
      "PL": "Serwer zostanie zrestartowany za 5 minut.",
      "DE": "Der Server wird in 5 Minuten neu gestartet."
    },
    "restart_in_1min": {
      "RU": "Сервер перезапустится через 1 минуту.",
      "US": "The server will restart in 1 minute.",
      "UA": "Сервер перезапуститься через 1 хвилину.",
      "PL": "Serwer zostanie zrestartowany za 1 minute.",
      "DE": "Der Server wird in 1 Minute neu gestartet."
    },
    "restart_in_30sec": {
      "RU": "Сервер перезапустится через 30 секунд.",
      "US": "The server will restart in 30 seconds.",
      "UA": "Сервер перезапуститься через 30 секунд.",
      "PL": "Serwer zostanie zrestartowany za 30 sekund.",
      "DE": "Der Server wird in 30 Sekunden neu gestartet."
    },
    "restart_in_10sec": {
      "RU": "Сервер перезапустится через 10 секунд.",
      "US": "The server will restart in 10 seconds.",
      "UA": "Сервер перезапуститься через 10 секунд.",
      "PL": "Serwer zostanie zrestartowany za 10 sekund.",
      "DE": "Der Server wird in 10 Sekunden neu gestartet."
    },
    "restart_in_seconds": {
      "RU": "Сервер перезапустится через {SECONDS} сек.",
      "US": "The server will restart in {SECONDS} sec.",
      "UA": "Сервер перезапуститься через {SECONDS} сек.",
      "PL": "Serwer zostanie zrestartowany za {SECONDS} sek.",
      "DE": "Der Server wird in {SECONDS} Sek. neu gestartet."
    },
    "restart_now": {
      "RU": "Сервер перезапускается.",
      "US": "The server is restarting.",
      "UA": "Сервер перезапускається.",
      "PL": "Serwer jest restartowany.",
      "DE": "Der Server wird neu gestartet."
    },
    "player": {
      "RU": "Игрок",
      "US": "Player",
      "UA": "Гравець",
      "PL": "Gracz",
      "DE": "Spieler"
    },
    "connected": {
      "RU": "{GREEN}Подключился ➡{DEFAULT}",
      "US": "{GREEN}Connected ➡{DEFAULT}",
      "UA": "{GREEN}Підключився ➡{DEFAULT}",
      "PL": "{GREEN}Połączony ➡{DEFAULT}",
      "DE": "{GREEN}Verbunden ➡{DEFAULT}"
    },
    "disconnected": {
      "RU": "{RED}Отключился ➡{DEFAULT}",
      "US": "{RED}Disconnected ➡{DEFAULT}",
      "UA": "{RED}Відключився ➡{DEFAULT}",
      "PL": "{RED}Rozłączył się ➡{DEFAULT}",
      "DE": "{RED}Getrennt ➡{DEFAULT}"
    },
    "announce_servers": {
      "RU": "Наши сервера:",
      "US": "Our servers:",
      "UA": "Наші сервери:",
      "PL": "Nasze serwery:",
      "DE": "Unsere Server:"
    },
    "welcome_player": {
      "RU": "Добро пожаловать",
      "US": "Welcome",
      "UA": "Ласкаво просимо",
      "PL": "Witamy",
      "DE": "Willkommen"
    },
    "welcome_text": {
      "RU": "на игровой сервер {RED}{SERVERNAME}",
      "US": "to the game server {RED}{SERVERNAME}",
      "UA": "на ігровий сервер {RED}{SERVERNAME}",
      "PL": "na serwer gry {RED}{SERVERNAME}",
      "DE": "auf den Spieleserver {RED}{SERVERNAME}"
    },
    "skins_title": {
      "RU": "Скины и ножи",
      "US": "Skins & Knives",
      "UA": "Скіни та ножі",
      "PL": "Skiny i noże",
      "DE": "Skins & Messer"
    },
    "viptest_title": {
      "RU": "Попробуй бесплатно: !viptest",
      "US": "Try it for free: !viptest",
      "UA": "Спробуй безкоштовно: !viptest",
      "PL": "Wypróbuj za darmo: !viptest",
      "DE": "Kostenlos testen: !viptest"
    },
    "reklama_1": {
      "RU": "Хочешь крутые скины? Используй команды:\nㅤㅤㅤ{LIGHTBLUE}➡ !ws\nㅤㅤㅤ➡ !knife\nㅤㅤㅤ➡ !gloves\nㅤㅤㅤ➡ !skins",
      "US": "Want awesome skins? Use commands:\nㅤㅤㅤ{LIGHTBLUE}➡ !ws\nㅤㅤㅤ➡ !knife\nㅤㅤㅤ➡ !gloves\nㅤㅤㅤ➡ !skins",
      "UA": "Хочеш круті скіни? Використовуй команди:\nㅤㅤㅤ{LIGHTBLUE}➡ !ws\nㅤㅤㅤ➡ !knife\nㅤㅤㅤ➡ !gloves\nㅤㅤㅤ➡ !skins",
      "PL": "Chcesz świetne skiny? Użyj komend:\nㅤㅤㅤ{LIGHTBLUE}➡ !ws\nㅤㅤㅤ➡ !knife\nㅤㅤㅤ➡ !gloves\nㅤㅤㅤ➡ !skins",
      "DE": "Willst du coole Skins? Nutze die Befehle:\nㅤㅤㅤ{LIGHTBLUE}➡ !ws\nㅤㅤㅤ➡ !knife\nㅤㅤㅤ➡ !gloves\nㅤㅤㅤ➡ !skins"
    },
    "reklama_2": {
      "RU": "Хочешь попробовать VIP? Активируй бесплатно на час:\nㅤㅤㅤ{RED}➡ !viptest",
      "US": "Want to try VIP? Activate for free for 1 hour:\nㅤㅤㅤ{RED}➡ !viptest",
      "UA": "Хочеш спробувати VIP? Активуй безкоштовно на годину:\nㅤㅤㅤ{RED}➡ !viptest",
      "PL": "Chcesz przetestować VIP? Aktywuj za darmo na godzinę:\nㅤㅤㅤ{RED}➡ !viptest",
      "DE": "VIP testen? Aktiviere es für eine Stunde kostenlos:\nㅤㅤㅤ{RED}➡ !viptest"
    },
    "reklama_3": {
      "RU": "Общайся, находи тиммейтов и узнавай новости в нашем Discord:\nㅤㅤㅤ{RED}➡ discord.gg/CHANGE-ME",
      "US": "Chat, find teammates, and stay updated in our Discord:\nㅤㅤㅤ{RED}➡ discord.gg/CHANGE-ME",
      "UA": "Спілкуйся, знаходь тіммейтів та дізнавайся новини в нашому Discord:\nㅤㅤㅤ{RED}➡ discord.gg/CHANGE-ME",
      "PL": "Rozmawiaj, znajdź drużynę i bądź na bieżąco na naszym Discordzie:\nㅤㅤㅤ{RED}➡ discord.gg/CHANGE-ME",
      "DE": "Chatte, finde Teammates und bleibe informiert auf unserem Discord:\nㅤㅤㅤ{RED}➡ discord.gg/CHANGE-ME"
    },
    "reklama_4": {
      "RU": "Хотите персональный стиль? Собери сет скинов на\nㅤㅤㅤ{RED}➡ your-site.example",
      "US": "Want your own style? Customize your skins at\nㅤㅤㅤ{RED}➡ your-site.example",
      "UA": "Хочеш власний стиль? Створюй свій сет скінів на\nㅤㅤㅤ{RED}➡ your-site.example",
      "PL": "Chcesz własny styl? Skonfiguruj swoje skiny na\nㅤㅤㅤ{RED}➡ your-site.example",
      "DE": "Dein eigener Stil? Erstelle dein Skin-Set auf\nㅤㅤㅤ{RED}➡ your-site.example"
    },
    "reklama_5": {
      "RU": "Видел читера? Сообщи о нем командой:\nㅤㅤㅤ{RED}➡ !report",
      "US": "Saw a cheater? Report them using:\nㅤㅤㅤ{RED}➡ !report",
      "UA": "Побачив чітера? Повідом командою:\nㅤㅤㅤ{RED}➡ !report",
      "PL": "Widziałeś cheatera? Zgłoś go za pomocą:\nㅤㅤㅤ{RED}➡ !report",
      "DE": "Hast du einen Cheater gesehen? Melde ihn mit:\nㅤㅤㅤ{RED}➡ !report"
    },
    "reklama_6": {
      "RU": "Посмотреть список серверов:\nㅤㅤㅤ{RED}➡ !servers",
      "US": "View server list:\nㅤㅤㅤ{RED}➡ !servers",
      "UA": "Переглянути список серверів:\nㅤㅤㅤ{RED}➡ !servers",
      "PL": "Zobacz listę serwerów:\nㅤㅤㅤ{RED}➡ !servers",
      "DE": "Serverliste anzeigen:\nㅤㅤㅤ{RED}➡ !servers"
    }
  },
  "JoinMessages": {
    "RU": [
      "{player} {connected} Страна: {country}, Город: {city}"
    ],
    "US": [
      "{player} {connected} Country: {country}, City: {city}"
    ],
    "UA": [
      "{player} {connected} Країна: {country}, Місто: {city}"
    ],
    "PL": [
      "{player} {connected} Kraj: {country}, Miasto: {city}"
    ],
    "DE": [
      "{player} {connected} Land: {country}, Stadt: {city}"
    ]
  },
  "LeaveMessages": {
    "RU": [
      "{player} {disconnected}"
    ],
    "US": [
      "{player} {disconnected}"
    ],
    "UA": [
      "{player} {disconnected}"
    ],
    "PL": [
      "{player} {disconnected}"
    ],
    "DE": [
      "{player} {disconnected}"
    ]
  }
})NM";
}

const char* AdsJson() {
    return R"NM({
  "$schema": "./Ads.schema.json",
  "Ads": [
    {
      "Interval": 120,
      "Messages": [
        {
          "Chat": "{prefix}{reklama_1}"
        },
        {
          "CenterHtml": "{BIG}{LIGHTBLUE}{skins_title}{DEFAULT}\n{SMALL}!ws • !knife • !gloves • !skins"
        }
      ]
    },
    {
      "Interval": 180,
      "Messages": [
        {
          "Chat": "{prefix}{reklama_2}"
        },
        {
          "CenterHtml": "{BIG}{GOLD}VIP{DEFAULT}\n{SMALL}{viptest_title}"
        }
      ]
    },
    {
      "Interval": 240,
      "Messages": [
        {
          "Chat": "{prefix}{reklama_3}"
        }
      ]
    },
    {
      "Interval": 300,
      "Messages": [
        {
          "Chat": "{prefix}{reklama_4}"
        }
      ]
    },
    {
      "Interval": 360,
      "Messages": [
        {
          "Chat": "{prefix}{reklama_5}"
        }
      ]
    },
    {
      "Interval": 420,
      "Messages": [
        {
          "Chat": "{prefix}{reklama_6}"
        }
      ]
    }
  ]
})NM";
}

const char* ServersJson() {
    return R"NM({
  "$schema": "./Servers.schema.json",
  "Enabled": false,
  "Interval": 60,
  "QueryTimeoutMs": 500,
  "CacheTtlSeconds": 30,
  "List": [
    {
      "Ip": "123.45.67.89",
      "Port": 27015,
      "MessageTemplate": "{LIGHTBLUE}[SERVER 1]{DEFAULT} {SERVER_MAP} | {GREEN}{SERVER_PLAYERS}{DEFAULT}/{SERVER_MAXPLAYERS}",
      "MessageTemplateConsole": "Server 1: {SERVER_IP}:{SERVER_PORT} - {SERVER_MAP} | Players: {SERVER_PLAYERS}/{SERVER_MAXPLAYERS}",
      "MaxPlayersFallback": 32
    },
    {
      "Ip": "123.45.67.90",
      "Port": 27015,
      "MessageTemplate": "{LIGHTBLUE}[SERVER 2]{DEFAULT} {SERVER_MAP} | {GREEN}{SERVER_PLAYERS}{DEFAULT}/{SERVER_MAXPLAYERS}",
      "MessageTemplateConsole": "Server 2: {SERVER_IP}:{SERVER_PORT} - {SERVER_MAP} | Players: {SERVER_PLAYERS}/{SERVER_MAXPLAYERS}",
      "MaxPlayersFallback": 32
    }
  ]
})NM";
}

const char* SettingsSchema() {
    return R"NM({
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "NotifyMessages — Settings.json",
  "description": "Основные настройки. Тексты сюда не пишут: здесь только ссылки на ключи из Messages.json вида {prefix} и {welcome_player}.",
  "type": "object",
  "properties": {
    "Debug": {
      "type": "boolean",
      "description": "Подробные логи в консоль сервера. Пишет SteamID, ники и гео игроков — по умолчанию выключено.",
      "default": false
    },
    "DefaultLang": {
      "type": "string",
      "description": "Язык, который увидит игрок, если его язык не определён или его нет в Messages.json.",
      "default": "RU"
    },
    "PrintToCenterHtml": {
      "type": ["boolean", "null"],
      "description": "УСТАРЕЛО. Поднимает весь обычный Center до CenterHtml. Вместо этого укажите MessageType: \"CenterHtml\" там, где нужна разметка."
    },
    "ShowHtmlWhenDead": {
      "type": ["boolean", "null"],
      "description": "Показывать ли HTML-центр мёртвым игрокам. Пока игрок мёртв, таймер показа не идёт.",
      "default": false
    },
    "HtmlCenterDuration": {
      "type": ["number", "null"],
      "description": "Сколько секунд держать сообщение в HTML-центре. Не задано — 5 секунд.",
      "minimum": 0.5
    },
    "WelcomeMessage": {
      "type": "object",
      "description": "Приветствие при заходе на сервер. Доступен тег {PLAYERNAME}.",
      "properties": {
        "MessageType": { "$ref": "#/definitions/messageType" },
        "Message": { "type": "string", "description": "Шаблон. Пример: {prefix}{welcome_player} {RED}{PLAYERNAME}" },
        "DisplayDelay": { "type": "number", "description": "Задержка перед показом, секунды.", "minimum": 0 }
      }
    },
    "ChangeTeamMessage": { "type": "string", "description": "Смена команды. Доступны {PLAYERNAME}, {TEAM}, {OLD_TEAM}." },
    "JoinTeamMessage": { "type": "string", "description": "Вход в команду. Доступны {PLAYERNAME}, {TEAM}." },
    "TitleAnnounceServers": { "type": "string", "description": "Заголовок списка серверов для команды !servers." },
    "RestartNotify": {
      "type": "object",
      "description": "Оповещение о рестарте: точка интеграции с внешним апдейтером через mm_restart_notify <секунды>.",
      "properties": {
        "Enabled": { "type": "boolean", "default": true },
        "MessageType": { "$ref": "#/definitions/messageType" },
        "DefaultMessage": {
          "type": "string",
          "description": "Шаблон для секунд, которых нет в Thresholds. Доступны {SECONDS} и {TIME_RESTART}."
        },
        "Thresholds": {
          "type": "object",
          "description": "Точные отсечки: секунды (строкой) -> шаблон. Совпадение только точное, «ближайший» порог не подбирается.",
          "additionalProperties": { "type": "string" }
        }
      }
    },
    "LanguageAliases": {
      "type": "object",
      "description": "Блок из Messages.json -> коды языков и стран, которые на него отображаются. Например \"RU\": [\"ru\", \"kk\", \"KZ\"] — игрок с русским клиентом или из Казахстана получит блок RU, дублировать переводы под каждую страну не нужно.",
      "additionalProperties": {
        "type": "array",
        "items": { "type": "string" }
      }
    },
    "MapsName": {
      "type": "object",
      "description": "Красивые имена карт: de_dust2 -> Dust 2. Подставляются в любом сообщении, где встретилось системное имя карты.",
      "additionalProperties": { "type": "string" }
    }
  },
  "definitions": {
    "messageType": {
  "description": "Канал вывода. Chat — чат; Center — обычный центр экрана (без цветов); CenterHtml — центр с разметкой (цвета и переносы строк работают); Console — консоль игрока; Alert — центральное предупреждение.",
  "enum": ["Chat", "Center", "CenterHtml", "Console", "Alert", 0, 1, 2, 3, 4],
  "default": "Chat"
}
  }
})NM";
}

const char* MessagesSchema() {
    return R"NM({
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "NotifyMessages — Messages.json",
  "description": "Единственное место, где живут тексты и переводы. В остальных файлах на них ссылаются ключами вида {prefix}.",
  "type": "object",
  "properties": {
    "LanguageMessages": {
      "type": "object",
      "description": "Ключ -> язык -> текст. Ключ используется в других файлах как {ключ}. В тексте можно использовать цветовые теги ({RED}, {GREEN}, ...) и системные ({MAP}, {PLAYERS}, ...).",
      "additionalProperties": {
        "type": "object",
        "additionalProperties": { "type": "string" }
      }
    },
    "JoinMessages": {
      "type": "object",
      "description": "Сообщения о заходе игрока: язык -> список вариантов, из которых выбирается случайный. Доступны {PLAYERNAME}, {COUNTRY}, {CITY}.",
      "additionalProperties": {
        "type": "array",
        "items": { "type": "string" }
      }
    },
    "LeaveMessages": {
      "type": "object",
      "description": "Сообщения о выходе игрока: язык -> список вариантов. Доступны {PLAYERNAME}, {COUNTRY}, {CITY}.",
      "additionalProperties": {
        "type": "array",
        "items": { "type": "string" }
      }
    }
  }
})NM";
}

const char* AdsSchema() {
    return R"NM({
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "NotifyMessages — Ads.json",
  "description": "Блоки рекламы. Каждый блок крутит свои сообщения по кругу со своим интервалом.",
  "type": "object",
  "properties": {
    "Ads": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "Interval": {
            "type": "number",
            "description": "Интервал показа блока в секундах.",
            "minimum": 1,
            "default": 120
          },
          "Messages": {
            "type": "array",
            "description": "Сообщения блока: показываются по очереди, по одному за срабатывание таймера.",
            "items": {
              "type": "object",
              "description": "Канал -> текст. Можно указать несколько каналов сразу — тогда сообщение уйдёт в каждый.",
              "properties": {
                "Chat": { "type": "string" },
                "Center": { "type": "string", "description": "Обычный центр экрана: цвета здесь не работают, теги будут убраны." },
                "CenterHtml": { "type": "string", "description": "Центр экрана с разметкой: цвета и переносы строк работают." },
                "Console": { "type": "string" },
                "Alert": { "type": "string" }
              },
              "additionalProperties": false
            }
          }
        }
      }
    }
  }
})NM";
}

const char* ServersSchema() {
    return R"NM({
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "NotifyMessages — Servers.json",
  "description": "Мониторинг чужих серверов по A2S для команды !servers.",
  "type": "object",
  "properties": {
    "Enabled": {
      "type": "boolean",
      "description": "Без true команда !servers ничего не покажет.",
      "default": false
    },
    "Interval": {
      "type": "number",
      "description": "Как часто опрашивать серверы, секунды.",
      "minimum": 10,
      "default": 60
    },
    "QueryTimeoutMs": {
      "type": "integer",
      "description": "Таймаут одного A2S-запроса, миллисекунды. Сервер показывает OFFLINE — увеличьте.",
      "minimum": 100,
      "maximum": 5000,
      "default": 500
    },
    "CacheTtlSeconds": {
      "type": "integer",
      "description": "Сколько секунд считать данные свежими.",
      "minimum": 0,
      "default": 30
    },
    "List": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "Ip": { "type": "string", "description": "IP или hostname." },
          "Port": { "type": "integer", "minimum": 1, "maximum": 65535, "default": 27015 },
          "MessageTemplate": {
            "type": "string",
            "description": "Строка для чата. Доступны {SERVER_IP}, {SERVER_PORT}, {SERVER_MAP}, {SERVER_PLAYERS}, {SERVER_MAXPLAYERS}."
          },
          "MessageTemplateConsole": {
            "type": "string",
            "description": "Строка для консоли игрока. Те же плейсхолдеры."
          },
          "MaxPlayersFallback": {
            "type": ["integer", "null"],
            "description": "Что показать в {SERVER_MAXPLAYERS}, если сервер не ответил."
          }
        }
      }
    }
  }
})NM";
}

const char* Readme() {
    return R"NM(NotifyMessages — конфигурация
==============================================================================

Settings.json   основные настройки (тексты сюда не пишут — только ключи)
Messages.json   все тексты и переводы: ключ -> язык -> строка
Ads.json        блоки рекламы
Servers.json    мониторинг других серверов для команды !servers

Рядом лежат *.schema.json. Откройте конфиг в редакторе с поддержкой JSON Schema
(например VS Code) — он будет подсказывать имена полей, допустимые значения
и подсвечивать опечатки. Это заменяет подробную документацию: она устаревает,
схема — нет, потому что обновляется вместе с плагином.

ЧТО ДЕЛАТЬ ПОСЛЕ ПРАВКИ
------------------------------------------------------------------------------
  mm_nm_check                 проверить шаблоны: неизвестные теги, дыры в переводах
  mm_nm_preview welcome       показать, как выглядит приветствие
  mm_nm_preview ad 1          показать первый блок рекламы
  mm_nm_preview key prefix    показать один ключ из Messages.json
  mm_nm_preview raw {RED}тест произвольный текст с тегами
  mm_reload_advert            применить изменения всех четырёх файлов

Команды mm_* выполняются в консоли сервера или через rcon: своей системы прав
у Metamod нет. Игрокам доступна только !servers.

Проверять правку перезапуском сервера или ожиданием интервала рекламы не нужно.

КАНАЛЫ ВЫВОДА
------------------------------------------------------------------------------
  Chat         чат. Цвета работают
  Center       центр экрана, обычный текст. Цветовые теги будут убраны
  CenterHtml   центр экрана с разметкой. Цвета и переносы строк работают
  Console      консоль игрока
  Alert        центральное предупреждение

Канал задаётся полем MessageType (Settings.json) или ключом объекта (Ads.json).
Пишется словом: "CenterHtml". Старые числовые значения тоже читаются.

ЦВЕТА И ПЛЕЙСХОЛДЕРЫ
------------------------------------------------------------------------------
Цвета: {DEFAULT} {WHITE} {RED} {DARKRED} {LIGHTRED} {GREEN} {LIME} {OLIVE}
       {YELLOW} {LIGHTYELLOW} {GOLD} {ORANGE} {BLUE} {LIGHTBLUE} {DARKBLUE}
       {PURPLE} {LIGHTPURPLE} {MAGENTA} {GREY} {SILVER} {BLUEGREY}
Размер (только CenterHtml): {BIG} {MEDIUM} {SMALL}
Прочее: {SPACE} — широкий пробел, \n — перенос строки

Всегда доступны: {MAP} {TIME} {DATE} {SERVERNAME} {IP} {PORT} {PLAYERS} {MAXPLAYERS}
Только в своих местах: {PLAYERNAME} {TEAM} {OLD_TEAM} {SECONDS} {TIME_RESTART}
                       {COUNTRY} {CITY} {SERVER_MAP} {SERVER_PLAYERS} ...
Где какой работает — скажет mm_nm_check.

ЕСЛИ ЧТО-ТО НЕ РАБОТАЕТ
------------------------------------------------------------------------------
Тег виден игроку как текст в скобках   -> mm_nm_check покажет, где он лишний
!servers молчит                        -> Servers.json: "Enabled": true и непустой List
Серверы показывают OFFLINE             -> проверьте IP/порт, поднимите QueryTimeoutMs
Битый JSON                             -> плагин не падает, а берёт значения по умолчанию
                                          и пишет в консоль файл, строку и позицию ошибки

Полное описание: https://github.com/Armatura-Create/NotifyMessagesCS2
==============================================================================
)NM";
}

}  // namespace defaults
}  // namespace nm
