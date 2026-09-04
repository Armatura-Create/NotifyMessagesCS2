using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NotifyMessages;

/// Значения по умолчанию для четырёх конфигов и текст README.txt.
///
/// Вынесены из ConfigService, потому что это ~650 строк данных, а не логики:
/// в основном файле остаётся загрузка, диагностика и склейка частей.
/// Применяются ровно один раз — при первом запуске, когда конфигов ещё нет.
public sealed partial class ConfigService
{
    /// Дефолтная конфигурация целиком — ровно та, что пишется при первом запуске.
    /// internal ради теста: дефолты обязаны быть чистыми с точки зрения диагностики шаблонов,
    /// иначе первый же запуск показывает игрокам теги в фигурных скобках.
    internal static Config BuildDefaultConfig() => MergeParts(
        CreateDefaultSettings(), CreateDefaultMessages(), CreateDefaultAds(), CreateDefaultServers());

    private static SettingsConfig CreateDefaultSettings()
    {
        return new SettingsConfig
        {
            // Выключен по умолчанию: Debug пишет в консоль SteamID, ник и гео каждого игрока
            Debug = false,
            DefaultLang = "RU",
            PrintToCenterHtml = false,
            ShowHtmlWhenDead = null,
            HtmlCenterDuration = null,
            
            WelcomeMessage = new WelcomeMessage
            {
                MessageType = MessageType.Chat,
                Message = "{prefix}{welcome_player} {RED}{PLAYERNAME} {DEFAULT}{welcome_text}",
                DisplayDelay = 5
            },
            
            ChangeTeamMessage = "{prefix}{changeTeamMessage}",
            JoinTeamMessage = "{prefix}{joinTeamMessage}",
            TitleAnnounceServers = "{prefix}{announce_servers}",

            RestartNotify = new RestartNotifyConfig
            {
                Enabled = true,
                MessageType = MessageType.Chat,
                DefaultMessage = "{prefix}{RED}{restart_in_seconds}",
                Thresholds = new Dictionary<string, string>
                {
                    ["300"] = "{prefix}{RED}{update_available} {DEFAULT}{restart_in_5min}",
                    ["60"] = "{prefix}{RED}{update_available} {DEFAULT}{restart_in_1min}",
                    ["30"] = "{prefix}{RED}{restart_in_30sec}",
                    ["10"] = "{prefix}{RED}{restart_in_10sec}",
                    ["5"] = "{prefix}{RED}{restart_in_seconds}",
                    ["4"] = "{prefix}{RED}{restart_in_seconds}",
                    ["3"] = "{prefix}{RED}{restart_in_seconds}",
                    ["2"] = "{prefix}{RED}{restart_in_seconds}",
                    ["1"] = "{prefix}{RED}{restart_now}"
                }
            },

            
            // Один блок переводов обслуживает несколько стран и языковых кодов.
            // Игрок с русским клиентом или из соседней страны не должен видеть DefaultLang
            // только потому, что блока под его ISO-код в конфиге нет.
            LanguageAliases = new Dictionary<string, List<string>>
            {
                ["RU"] = new() { "ru", "be", "kk", "BY", "KZ", "MD", "AM", "KG", "UZ" },
                ["US"] = new() { "en", "GB", "CA", "AU", "NZ", "IE" },
                ["UA"] = new() { "uk" },
                ["PL"] = new() { "pl" },
                ["DE"] = new() { "de", "AT", "CH" }
            },

            MapsName = new Dictionary<string, string>
            {
                ["de_dust2"] = "Dust 2",
                ["de_mirage"] = "Mirage",
                ["de_inferno"] = "Inferno",
                ["de_nuke"] = "Nuke",
                ["de_overpass"] = "Overpass",
                ["de_vertigo"] = "Vertigo",
                ["de_ancient"] = "Ancient",
                ["de_anubis"] = "Anubis"
            }
        };
    }

    private static MessagesConfig CreateDefaultMessages()
    {
        return new MessagesConfig
        {
            LanguageMessages = new Dictionary<string, Dictionary<string, string>>
            {
                ["prefix"] = new()
                {
                    ["RU"] = "{LIGHTBLUE}Server ➡{DEFAULT} ",
                    ["US"] = "{LIGHTBLUE}Server ➡{DEFAULT} ",
                    ["UA"] = "{LIGHTBLUE}Server ➡{DEFAULT} ",
                    ["PL"] = "{LIGHTBLUE}Server ➡{DEFAULT} ",
                    ["DE"] = "{LIGHTBLUE}Server ➡{DEFAULT} "
                },
                
                // Команды и игроки
                ["changeTeamMessage"] = new()
                {
                    ["RU"] = "{GREEN}{PLAYERNAME}{DEFAULT} перешел из команды {BLUE}{OLD_TEAM} в команду {BLUE}{TEAM}",
                    ["US"] = "{GREEN}{PLAYERNAME}{DEFAULT} switched from {BLUE}{OLD_TEAM} to {BLUE}{TEAM}",
                    ["UA"] = "{GREEN}{PLAYERNAME}{DEFAULT} перейшов з команди {BLUE}{OLD_TEAM} до команди {BLUE}{TEAM}",
                    ["PL"] = "{GREEN}{PLAYERNAME}{DEFAULT} przeszedł z drużyny {BLUE}{OLD_TEAM} do drużyny {BLUE}{TEAM}",
                    ["DE"] = "{GREEN}{PLAYERNAME}{DEFAULT} wechselte von {BLUE}{OLD_TEAM} zu {BLUE}{TEAM}"
                },
                ["joinTeamMessage"] = new()
                {
                    ["RU"] = "{GREEN}{PLAYERNAME}{DEFAULT} присоединился к {TEAM}",
                    ["US"] = "{GREEN}{PLAYERNAME}{DEFAULT} joined {TEAM}",
                    ["UA"] = "{GREEN}{PLAYERNAME}{DEFAULT} приєднався до {TEAM}",
                    ["PL"] = "{GREEN}{PLAYERNAME}{DEFAULT} dołączył do {TEAM}",
                    ["DE"] = "{GREEN}{PLAYERNAME}{DEFAULT} trat {TEAM} bei"
                },
                
                // Оповещение о рестарте (css_restart_notify)
                ["update_available"] = new()
                {
                    ["RU"] = "Вышло обновление CS2!",
                    ["US"] = "A new CS2 update is available!",
                    ["UA"] = "Вийшло оновлення CS2!",
                    ["PL"] = "Dostepna jest nowa aktualizacja CS2!",
                    ["DE"] = "Ein neues CS2-Update ist verfugbar!"
                },
                ["restart_in_5min"] = new()
                {
                    ["RU"] = "Сервер перезапустится через 5 минут.",
                    ["US"] = "The server will restart in 5 minutes.",
                    ["UA"] = "Сервер перезапуститься через 5 хвилин.",
                    ["PL"] = "Serwer zostanie zrestartowany za 5 minut.",
                    ["DE"] = "Der Server wird in 5 Minuten neu gestartet."
                },
                ["restart_in_1min"] = new()
                {
                    ["RU"] = "Сервер перезапустится через 1 минуту.",
                    ["US"] = "The server will restart in 1 minute.",
                    ["UA"] = "Сервер перезапуститься через 1 хвилину.",
                    ["PL"] = "Serwer zostanie zrestartowany za 1 minute.",
                    ["DE"] = "Der Server wird in 1 Minute neu gestartet."
                },
                ["restart_in_30sec"] = new()
                {
                    ["RU"] = "Сервер перезапустится через 30 секунд.",
                    ["US"] = "The server will restart in 30 seconds.",
                    ["UA"] = "Сервер перезапуститься через 30 секунд.",
                    ["PL"] = "Serwer zostanie zrestartowany za 30 sekund.",
                    ["DE"] = "Der Server wird in 30 Sekunden neu gestartet."
                },
                ["restart_in_10sec"] = new()
                {
                    ["RU"] = "Сервер перезапустится через 10 секунд.",
                    ["US"] = "The server will restart in 10 seconds.",
                    ["UA"] = "Сервер перезапуститься через 10 секунд.",
                    ["PL"] = "Serwer zostanie zrestartowany za 10 sekund.",
                    ["DE"] = "Der Server wird in 10 Sekunden neu gestartet."
                },
                ["restart_in_seconds"] = new()
                {
                    ["RU"] = "Сервер перезапустится через {SECONDS} сек.",
                    ["US"] = "The server will restart in {SECONDS} sec.",
                    ["UA"] = "Сервер перезапуститься через {SECONDS} сек.",
                    ["PL"] = "Serwer zostanie zrestartowany za {SECONDS} sek.",
                    ["DE"] = "Der Server wird in {SECONDS} Sek. neu gestartet."
                },
                ["restart_now"] = new()
                {
                    ["RU"] = "Сервер перезапускается.",
                    ["US"] = "The server is restarting.",
                    ["UA"] = "Сервер перезапускається.",
                    ["PL"] = "Serwer jest restartowany.",
                    ["DE"] = "Der Server wird neu gestartet."
                },

                ["player"] = new()
                {
                    ["RU"] = "Игрок",
                    ["US"] = "Player",
                    ["UA"] = "Гравець",
                    ["PL"] = "Gracz",
                    ["DE"] = "Spieler"
                },
                ["connected"] = new()
                {
                    ["RU"] = "{GREEN}Подключился ➡{DEFAULT}",
                    ["US"] = "{GREEN}Connected ➡{DEFAULT}",
                    ["UA"] = "{GREEN}Підключився ➡{DEFAULT}",
                    ["PL"] = "{GREEN}Połączony ➡{DEFAULT}",
                    ["DE"] = "{GREEN}Verbunden ➡{DEFAULT}"
                },
                ["disconnected"] = new()
                {
                    ["RU"] = "{RED}Отключился ➡{DEFAULT}",
                    ["US"] = "{RED}Disconnected ➡{DEFAULT}",
                    ["UA"] = "{RED}Відключився ➡{DEFAULT}",
                    ["PL"] = "{RED}Rozłączył się ➡{DEFAULT}",
                    ["DE"] = "{RED}Getrennt ➡{DEFAULT}"
                },
                
                ["announce_servers"] = new()
                {
                    ["RU"] = "Наши сервера:",
                    ["US"] = "Our servers:",
                    ["UA"] = "Наші сервери:",
                    ["PL"] = "Nasze serwery:",
                    ["DE"] = "Unsere Server:"
                },
                
                ["welcome_player"] = new()
                {
                    ["RU"] = "Добро пожаловать",
                    ["US"] = "Welcome",
                    ["UA"] = "Ласкаво просимо",
                    ["PL"] = "Witamy",
                    ["DE"] = "Willkommen"
                },
                ["welcome_text"] = new()
                {
                    ["RU"] = "на игровой сервер {RED}{SERVERNAME}",
                    ["US"] = "to the game server {RED}{SERVERNAME}",
                    ["UA"] = "на ігровий сервер {RED}{SERVERNAME}",
                    ["PL"] = "na serwer gry {RED}{SERVERNAME}",
                    ["DE"] = "auf den Spieleserver {RED}{SERVERNAME}"
                },
                
                // Реклама (используется в Ads.json)
                ["reklama_1"] = new()
                {
                    ["RU"] = "Хочешь крутые скины? Используй команды:\nㅤㅤㅤ{LIGHTBLUE}➡ !ws\nㅤㅤㅤ➡ !knife\nㅤㅤㅤ➡ !gloves\nㅤㅤㅤ➡ !skins",
                    ["US"] = "Want awesome skins? Use commands:\nㅤㅤㅤ{LIGHTBLUE}➡ !ws\nㅤㅤㅤ➡ !knife\nㅤㅤㅤ➡ !gloves\nㅤㅤㅤ➡ !skins",
                    ["UA"] = "Хочеш круті скіни? Використовуй команди:\nㅤㅤㅤ{LIGHTBLUE}➡ !ws\nㅤㅤㅤ➡ !knife\nㅤㅤㅤ➡ !gloves\nㅤㅤㅤ➡ !skins",
                    ["PL"] = "Chcesz świetne skiny? Użyj komend:\nㅤㅤㅤ{LIGHTBLUE}➡ !ws\nㅤㅤㅤ➡ !knife\nㅤㅤㅤ➡ !gloves\nㅤㅤㅤ➡ !skins",
                    ["DE"] = "Willst du coole Skins? Nutze die Befehle:\nㅤㅤㅤ{LIGHTBLUE}➡ !ws\nㅤㅤㅤ➡ !knife\nㅤㅤㅤ➡ !gloves\nㅤㅤㅤ➡ !skins"
                },
                ["reklama_2"] = new()
                {
                    ["RU"] = "Хочешь попробовать VIP? Активируй бесплатно на час:\nㅤㅤㅤ{RED}➡ !viptest",
                    ["US"] = "Want to try VIP? Activate for free for 1 hour:\nㅤㅤㅤ{RED}➡ !viptest",
                    ["UA"] = "Хочеш спробувати VIP? Активуй безкоштовно на годину:\nㅤㅤㅤ{RED}➡ !viptest",
                    ["PL"] = "Chcesz przetestować VIP? Aktywuj za darmo na godzinę:\nㅤㅤㅤ{RED}➡ !viptest",
                    ["DE"] = "VIP testen? Aktiviere es für eine Stunde kostenlos:\nㅤㅤㅤ{RED}➡ !viptest"
                },
                ["reklama_3"] = new()
                {
                    ["RU"] = "Общайся, находи тиммейтов и узнавай новости в нашем Discord:\nㅤㅤㅤ{RED}➡ discord.gg/CHANGE-ME",
                    ["US"] = "Chat, find teammates, and stay updated in our Discord:\nㅤㅤㅤ{RED}➡ discord.gg/CHANGE-ME",
                    ["UA"] = "Спілкуйся, знаходь тіммейтів та дізнавайся новини в нашому Discord:\nㅤㅤㅤ{RED}➡ discord.gg/CHANGE-ME",
                    ["PL"] = "Rozmawiaj, znajdź drużynę i bądź na bieżąco na naszym Discordzie:\nㅤㅤㅤ{RED}➡ discord.gg/CHANGE-ME",
                    ["DE"] = "Chatte, finde Teammates und bleibe informiert auf unserem Discord:\nㅤㅤㅤ{RED}➡ discord.gg/CHANGE-ME"
                },
                ["reklama_4"] = new()
                {
                    ["RU"] = "Хотите персональный стиль? Собери сет скинов на\nㅤㅤㅤ{RED}➡ your-site.example",
                    ["US"] = "Want your own style? Customize your skins at\nㅤㅤㅤ{RED}➡ your-site.example",
                    ["UA"] = "Хочеш власний стиль? Створюй свій сет скінів на\nㅤㅤㅤ{RED}➡ your-site.example",
                    ["PL"] = "Chcesz własny styl? Skonfiguruj swoje skiny na\nㅤㅤㅤ{RED}➡ your-site.example",
                    ["DE"] = "Dein eigener Stil? Erstelle dein Skin-Set auf\nㅤㅤㅤ{RED}➡ your-site.example"
                },
                ["reklama_5"] = new()
                {
                    ["RU"] = "Видел читера? Сообщи о нем командой:\nㅤㅤㅤ{RED}➡ !report",
                    ["US"] = "Saw a cheater? Report them using:\nㅤㅤㅤ{RED}➡ !report",
                    ["UA"] = "Побачив чітера? Повідом командою:\nㅤㅤㅤ{RED}➡ !report",
                    ["PL"] = "Widziałeś cheatera? Zgłoś go za pomocą:\nㅤㅤㅤ{RED}➡ !report",
                    ["DE"] = "Hast du einen Cheater gesehen? Melde ihn mit:\nㅤㅤㅤ{RED}➡ !report"
                },
                ["reklama_6"] = new()
                {
                    ["RU"] = "Посмотреть список серверов:\nㅤㅤㅤ{RED}➡ !servers",
                    ["US"] = "View server list:\nㅤㅤㅤ{RED}➡ !servers",
                    ["UA"] = "Переглянути список серверів:\nㅤㅤㅤ{RED}➡ !servers",
                    ["PL"] = "Zobacz listę serwerów:\nㅤㅤㅤ{RED}➡ !servers",
                    ["DE"] = "Serverliste anzeigen:\nㅤㅤㅤ{RED}➡ !servers"
                }
            },
            
            JoinMessages = new Dictionary<string, List<string>>
            {
                ["RU"] = new() { "{player} {connected} Страна: {country}, Город: {city}" },
                ["US"] = new() { "{player} {connected} Country: {country}, City: {city}" },
                ["UA"] = new() { "{player} {connected} Країна: {country}, Місто: {city}" },
                ["PL"] = new() { "{player} {connected} Kraj: {country}, Miasto: {city}" },
                ["DE"] = new() { "{player} {connected} Land: {country}, Stadt: {city}" }
            },
            
            LeaveMessages = new Dictionary<string, List<string>>
            {
                ["RU"] = new() { "{player} {disconnected}" },
                ["US"] = new() { "{player} {disconnected}" },
                ["UA"] = new() { "{player} {disconnected}" },
                ["PL"] = new() { "{player} {disconnected}" },
                ["DE"] = new() { "{player} {disconnected}" }
            }
        };
    }

    private static AdsConfig CreateDefaultAds()
    {
        return new AdsConfig
        {
            Ads = new List<Advertisement>
            {
                new Advertisement
                {
                    Interval = 120,
                    Messages = new List<Dictionary<string, string>>
                    {
                        new() { ["Chat"] = "{prefix}{reklama_1}" },
                        new() { ["Center"] = "!ws • !knife • !gloves • !skins" }
                    }
                },
                new Advertisement
                {
                    Interval = 180,
                    Messages = new List<Dictionary<string, string>>
                    {
                        new() { ["Chat"] = "{prefix}{reklama_2}" },
                        new() { ["Center"] = "!viptest - FREE!" }
                    }
                },
                new Advertisement
                {
                    Interval = 240,
                    Messages = new List<Dictionary<string, string>>
                    {
                        new() { ["Chat"] = "{prefix}{reklama_3}" }
                    }
                },
                new Advertisement
                {
                    Interval = 300,
                    Messages = new List<Dictionary<string, string>>
                    {
                        new() { ["Chat"] = "{prefix}{reklama_4}" }
                    }
                },
                new Advertisement
                {
                    Interval = 360,
                    Messages = new List<Dictionary<string, string>>
                    {
                        new() { ["Chat"] = "{prefix}{reklama_5}" }
                    }
                },
                new Advertisement
                {
                    Interval = 420,
                    Messages = new List<Dictionary<string, string>>
                    {
                        new() { ["Chat"] = "{prefix}{reklama_6}" }
                    }
                }
            }
        };
    }

    private static ServersConfig CreateDefaultServers()
    {
        return new ServersConfig
        {
            Enabled = false,
            Interval = 60,
            QueryTimeoutMs = 500,
            CacheTtlSeconds = 30,
            List = new List<ServerData>
            {
                new ServerData
                {
                    Ip = "123.45.67.89",
                    Port = 27015,
                    MessageTemplate = "{LIGHTBLUE}[SERVER 1]{DEFAULT} {SERVER_MAP} | {GREEN}{SERVER_PLAYERS}{DEFAULT}/{SERVER_MAXPLAYERS}",
                    MessageTemplateConsole = "Server 1: {SERVER_IP}:{SERVER_PORT} - {SERVER_MAP} | Players: {SERVER_PLAYERS}/{SERVER_MAXPLAYERS}",
                    MaxPlayersFallback = 32
                },
                    new ServerData
                    {
                    Ip = "123.45.67.90",
                        Port = 27015,
                    MessageTemplate = "{LIGHTBLUE}[SERVER 2]{DEFAULT} {SERVER_MAP} | {GREEN}{SERVER_PLAYERS}{DEFAULT}/{SERVER_MAXPLAYERS}",
                    MessageTemplateConsole = "Server 2: {SERVER_IP}:{SERVER_PORT} - {SERVER_MAP} | Players: {SERVER_PLAYERS}/{SERVER_MAXPLAYERS}",
                    MaxPlayersFallback = 32
                }
            }
        };
    }

    // ---- README файл ----------------------------------------------------------

    /// Короткая шпаргалка рядом с конфигами.
    ///
    /// Раньше здесь было 230 строк, которые писались ровно один раз — при первом запуске —
    /// и после обновления плагина описывали старую версию. Подробности теперь живут
    /// в *.schema.json (редактор подсказывает прямо во время правки) и в README.md проекта,
    /// а файл перезаписывается при каждой загрузке.
    private static void CreateConfigReadme(string directory)
    {
        var readmePath = Path.Combine(directory, "README.txt");
        var content = @"NotifyMessages — конфигурация
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
  css_nm_check                 проверить шаблоны: неизвестные теги, дыры в переводах
  css_nm_preview welcome       показать приветствие себе прямо сейчас
  css_nm_preview ad 1          показать первый блок рекламы
  css_nm_preview key prefix    показать один ключ из Messages.json
  css_nm_preview raw {RED}тест произвольный текст с тегами
  css_reload_advert            применить изменения всех четырёх файлов

Проверять правку перезапуском сервера или ожиданием интервала рекламы не нужно.

КАНАЛЫ ВЫВОДА
------------------------------------------------------------------------------
  Chat         чат. Цвета работают
  Center       центр экрана, обычный текст. Цветовые теги будут убраны
  CenterHtml   центр экрана с разметкой. Цвета и переносы строк работают
  Console      консоль игрока
  Alert        центральное предупреждение

Канал задаётся полем MessageType (Settings.json) или ключом объекта (Ads.json).
Пишется словом: ""CenterHtml"". Старые числовые значения тоже читаются.

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
Где какой работает — скажет css_nm_check.

ЕСЛИ ЧТО-ТО НЕ РАБОТАЕТ
------------------------------------------------------------------------------
Тег виден игроку как текст в скобках   -> css_nm_check покажет, где он лишний
!servers молчит                        -> Servers.json: ""Enabled"": true и непустой List
Серверы показывают OFFLINE             -> проверьте IP/порт, поднимите QueryTimeoutMs
Битый JSON                             -> плагин не падает, а берёт значения по умолчанию
                                          и пишет в консоль файл, строку и позицию ошибки

Полное описание: https://github.com/Armaturix/NotifyMessages
==============================================================================
";

        File.WriteAllText(readmePath, content, Encoding.UTF8);
    }

    // ---- Валидация ------------------------------------------------------------
}
