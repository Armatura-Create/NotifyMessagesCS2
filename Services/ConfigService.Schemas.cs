using System.IO;
using System.Text;

namespace NotifyMessages;

/// JSON Schema для четырёх конфигов.
///
/// Смысл: редактор с поддержкой схем (VS Code и подобные) начинает подсказывать имена полей,
/// допустимые значения и подсвечивать опечатки прямо во время правки. Это заменяет
/// значительную часть документации — и, в отличие от неё, не устаревает молча:
/// файлы перезаписываются при каждой загрузке плагина.
public sealed partial class ConfigService
{
    private static void WriteSchemas(string directory)
    {
        Write(directory, "Settings.schema.json", SettingsSchema);
        Write(directory, "Messages.schema.json", MessagesSchema);
        Write(directory, "Ads.schema.json", AdsSchema);
        Write(directory, "Servers.schema.json", ServersSchema);
    }

    private static void Write(string directory, string fileName, string content)
        => File.WriteAllText(Path.Combine(directory, fileName), content, Encoding.UTF8);

    private const string MessageTypeDefinition = """
        "messageType": {
          "description": "Канал вывода. Chat — чат; Center — обычный центр экрана (без цветов); CenterHtml — центр с разметкой (цвета и переносы строк работают); Console — консоль игрока; Alert — центральное предупреждение.",
          "enum": ["Chat", "Center", "CenterHtml", "Console", "Alert", 0, 1, 2, 3, 4],
          "default": "Chat"
        }
        """;

    private static readonly string SettingsSchema = $$"""
        {
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
            "RestartMessage": { "type": "string", "description": "Шаблон для css_announce_restart. Доступен тег {TIME_RESTART}." },
            "UpdateMessage": { "type": "string", "description": "Шаблон для css_announce_update. Доступен тег {TIME_RESTART}." },
            "ChangeTeamMessage": { "type": "string", "description": "Смена команды. Доступны {PLAYERNAME}, {TEAM}, {OLD_TEAM}." },
            "JoinTeamMessage": { "type": "string", "description": "Вход в команду. Доступны {PLAYERNAME}, {TEAM}." },
            "TitleAnnounceServers": { "type": "string", "description": "Заголовок списка серверов для команды !servers." },
            "RestartNotify": {
              "type": "object",
              "description": "Оповещение о рестарте: точка интеграции с внешним апдейтером через css_restart_notify <секунды>.",
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
            {{MessageTypeDefinition}}
          }
        }
        """;

    private const string MessagesSchema = """
        {
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
        }
        """;

    private const string AdsSchema = """
        {
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
        }
        """;

    private const string ServersSchema = """
        {
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
        }
        """;
}
