using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NotifyMessages;

/// Сервис работы с конфигурацией плагина: загрузка/сохранение и создание дефолта
public sealed class ConfigService
{
    private readonly ILogger _logger;

    public ConfigService(ILogger logger)
    {
        _logger = logger;
    }

    // ---- Public API -----------------------------------------------------------

    /// Загружает конфигурацию из нескольких файлов (split) или из legacy-файла
    public Config LoadOrCreate(string rootDirectory)
    {
        var directory = Path.Combine(rootDirectory, "configs/plugins/NotifyMessages");
        Directory.CreateDirectory(directory);

        var settingsPath = Path.Combine(directory, "Settings.json");
        var adsPath = Path.Combine(directory, "Ads.json");
        var messagesPath = Path.Combine(directory, "Messages.json");
        var serversPath = Path.Combine(directory, "Servers.json");

        var splitExists = File.Exists(settingsPath) || File.Exists(adsPath) || File.Exists(messagesPath) || File.Exists(serversPath);

        if (!splitExists)
        {
            _logger.Info("═══════════════════════════════════════════════════════════════");
            _logger.Info("  NotifyMessages - First Run Detected!");
            _logger.Info("  Creating default configuration files...");
            _logger.Info("═══════════════════════════════════════════════════════════════");
            
            // Создаём split-конфиги по умолчанию
            return CreateDefaultSplitConfigs(directory);
        }

        // Загружаем части и объединяем
        var settings = LoadPartial(settingsPath);
        var ads = LoadPartial(adsPath);
        var messages = LoadPartial(messagesPath);
        var servers = LoadPartial(serversPath);

        var config = MergeParts(settings, ads, messages, servers);
        
        // Валидация и предупреждения
        ValidateConfig(config, directory);
        
        return config;
    }

    /// Создаёт дефолтные split-конфиги и возвращает объединённую конфигурацию
    public Config CreateDefaultConfig(string configPath)
    {
        var dir = Path.GetDirectoryName(configPath)!;
        return CreateDefaultSplitConfigs(dir);
    }

    // ---- Internal helpers -----------------------------------------------------

    private static Config? LoadPartial(string path)
    {
        if (!File.Exists(path)) return null;
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Config>(json,
            new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip });
    }

    private Config MergeParts(Config? settings, Config? ads, Config? messages, Config? servers)
    {
        // Собираем результирующую конфигурацию через объект‑инициализатор (учёт init‑свойств)
        var result = new Config
        {
            // Settings
            PrintToCenterHtml = settings?.PrintToCenterHtml,
            HtmlCenterDuration = settings?.HtmlCenterDuration,
            ShowHtmlWhenDead = settings?.ShowHtmlWhenDead,
            Debug = settings?.Debug ?? false,
            WelcomeMessage = settings?.WelcomeMessage,
            RestartMessage = settings?.RestartMessage,
            UpdateMessage = settings?.UpdateMessage,
            ChangeTeamMessage = settings?.ChangeTeamMessage,
            JoinTeamMessage = settings?.JoinTeamMessage,

            // Ads
            Ads = ads?.Ads,
            Panel = ads?.Panel,

            // Localization/maps
            DefaultLang = settings?.DefaultLang,
            LanguageMessages = settings?.LanguageMessages,
            MapsName = settings?.MapsName,

            // Join/leave messages
            JoinMessages = messages?.JoinMessages,
            LeaveMessages = messages?.LeaveMessages,

            // Servers
            TitleAnnounceServers = servers?.TitleAnnounceServers,
            Servers = servers?.Servers
        };

        return result;
    }

    private Config CreateDefaultSplitConfigs(string directory)
    {
        // Default values are taken from the legacy single-file config and split by domains
        var settings = new Config
        {
            Debug = false,
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
            RestartMessage = "{prefix}{RED}{will_restarted}",
            UpdateMessage = "{prefix}{RED}{will_updated}",
            DefaultLang = "RU",
            LanguageMessages = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>
            {
                ["will_restarted"] = new() {
                    ["RU"] = "Сервер будет перезапущен через {TIME_RESTART}!",
                    ["US"] = "The server will be restarted in {TIME_RESTART}!",
                    ["UA"] = "Сервер буде перезапущено через {TIME_RESTART}!",
                    ["PL"] = "Serwer zostanie ponownie uruchomiony za {TIME_RESTART}!",
                    ["DE"] = "Der Server wird in {TIME_RESTART} neu gestartet!"
                },
                ["will_updated"] = new() {
                    ["RU"] = "Вышло обновление! Сервер будет перезапущен через {TIME_RESTART}.",
                    ["US"] = "An update has been released! The server will restart in {TIME_RESTART}.",
                    ["UA"] = "Вийшло оновлення! Сервер буде перезапущено через {TIME_RESTART}.",
                    ["PL"] = "Wydano aktualizację! Serwer zostanie ponownie uruchomiony za {TIME_RESTART}.",
                    ["DE"] = "Ein Update wurde veröffentlicht! Der Server wird in {TIME_RESTART} neu gestartet."
                },
                ["changeTeamMessage"] = new() {
                    ["RU"] = "{GREEN}{PLAYERNAME}{DEFAULT} перешел из команды {BLUE}{OLD_TEAM} в команду {BLUE}{TEAM}",
                    ["US"] = "{GREEN}{PLAYERNAME}{DEFAULT} switched from {BLUE}{OLD_TEAM} to {BLUE}{TEAM}",
                    ["UA"] = "{GREEN}{PLAYERNAME}{DEFAULT} перейшов з команди {BLUE}{OLD_TEAM} до команди {BLUE}{TEAM}",
                    ["PL"] = "{GREEN}{PLAYERNAME}{DEFAULT} przeszedł z drużyny {BLUE}{OLD_TEAM} do drużyny {BLUE}{TEAM}",
                    ["DE"] = "{GREEN}{PLAYERNAME}{DEFAULT} wechselte von {BLUE}{OLD_TEAM} zu {BLUE}{TEAM}"
                },
                ["joinTeamMessage"] = new() {
                    ["RU"] = "{GREEN}{PLAYERNAME}{DEFAULT} присоединился к {TEAM}",
                    ["US"] = "{GREEN}{PLAYERNAME}{DEFAULT} joined {TEAM}",
                    ["UA"] = "{GREEN}{PLAYERNAME}{DEFAULT} приєднався до {TEAM}",
                    ["PL"] = "{GREEN}{PLAYERNAME}{DEFAULT} dołączył do {TEAM}",
                    ["DE"] = "{GREEN}{PLAYERNAME}{DEFAULT} trat {TEAM} bei"
                },
                ["player"] = new() {
                    ["RU"] = "Игрок",
                    ["US"] = "Player",
                    ["UA"] = "Гравець",
                    ["PL"] = "Gracz",
                    ["DE"] = "Spieler"
                },
                ["connected"] = new() {
                    ["RU"] = "{GREEN}Подключился ➡{DEFAULT}",
                    ["US"] = "{GREEN}Connected ➡{DEFAULT}",
                    ["UA"] = "{GREEN}Підключився ➡{DEFAULT}",
                    ["PL"] = "{GREEN}Połączony ➡{DEFAULT}",
                    ["DE"] = "{GREEN}Verbunden ➡{DEFAULT}"
                },
                ["disconnected"] = new() {
                    ["RU"] = "{RED}Отключился ➡{DEFAULT}",
                    ["US"] = "{RED}Dissconected ➡{DEFAULT}",
                    ["UA"] = "{RED}Відключився ➡{DEFAULT}",
                    ["PL"] = "{RED}Rozłączył się ➡{DEFAULT}",
                    ["DE"] = "{RED}Getrennt ➡{DEFAULT}"
                },
                ["announce_servers"] = new() {
                    ["RU"] = "Наши сервера:",
                    ["US"] = "Our servers:",
                    ["UA"] = "Наші сервери:",
                    ["PL"] = "Nasze serwery:",
                    ["DE"] = "Unsere Server:"
                },
                ["welcome_player"] = new() {
                    ["RU"] = "Добро пожаловать",
                    ["US"] = "Welcome",
                    ["UA"] = "Ласкаво просимо",
                    ["PL"] = "Witamy",
                    ["DE"] = "Willkommen"
                },
                ["prefix"] = new() {
                    ["RU"] = "{LIGHTBLUE}Armaturix ➡{DEFAULT} ",
                    ["US"] = "{LIGHTBLUE}Armaturix ➡{DEFAULT} ",
                    ["UA"] = "{LIGHTBLUE}Armaturix ➡{DEFAULT} ",
                    ["PL"] = "{LIGHTBLUE}Armaturix ➡{DEFAULT} ",
                    ["DE"] = "{LIGHTBLUE}Armaturix ➡{DEFAULT} "
                },
                ["welcome_text"] = new() {
                    ["RU"] = "на игровой сервер {RED}Armaturix",
                    ["US"] = "to the game server {RED}Armaturix",
                    ["UA"] = "на ігровий сервер {RED}Armaturix",
                    ["PL"] = "na serwer gry {RED}Armaturix",
                    ["DE"] = "auf den Spieleserver {RED}Armaturix"
                },
                ["reklama_1"] = new() {
                    ["RU"] = "Хочешь крутые скины? Используй команды:\nㅤㅤㅤ{LIGHTBLUE}➡ !ws\nㅤㅤㅤ➡ !knife\nㅤㅤㅤ➡ !gloves\nㅤㅤㅤ➡ !skins",
                    ["US"] = "Want awesome skins? Use commands:\nㅤㅤㅤ{LIGHTBLUE}➡ !ws\nㅤㅤㅤ➡ !knife\nㅤㅤㅤ➡ !gloves\nㅤㅤㅤ➡ !skins",
                    ["UA"] = "Хочеш круті скіни? Використовуй команди:\nㅤㅤㅤ{LIGHTBLUE}➡ !ws\nㅤㅤㅤ➡ !knife\nㅤㅤㅤ➡ !gloves\nㅤㅤㅤ➡ !skins",
                    ["PL"] = "Chcesz świetne skiny? Użyj komend:\nㅤㅤㅤ{LIGHTBLUE}➡ !ws\nㅤㅤㅤ➡ !knife\nㅤㅤㅤ➡ !gloves\nㅤㅤㅤ➡ !skins",
                    ["DE"] = "Willst du coole Skins? Nutze die Befehle:\nㅤㅤㅤ{LIGHTBLUE}➡ !ws\nㅤㅤㅤ➡ !knife\nㅤㅤㅤ➡ !gloves\nㅤㅤㅤ➡ !skins"
                },
                ["reklama_2"] = new() {
                    ["RU"] = "Хочешь попробовать VIP? Активируй бесплатно на час:\nㅤㅤㅤ{RED}➡ !viptest",
                    ["US"] = "Want to try VIP? Activate for free for 1 hour:\nㅤㅤㅤ{RED}➡ !viptest",
                    ["UA"] = "Хочеш спробувати VIP? Активуй безкоштовно на годину:\nㅤㅤㅤ{RED}➡ !viptest",
                    ["PL"] = "Chcesz przetestować VIP? Aktywuj za darmo na godzinę:\nㅤㅤㅤ{RED}➡ !viptest",
                    ["DE"] = "VIP testen? Aktiviere es für eine Stunde kostenlos:\nㅤㅤㅤ{RED}➡ !viptest"
                },
                ["reklama_3"] = new() {
                    ["RU"] = "Общайся, находи тиммейтов и узнавай новости в нашем Discord:\nㅤㅤㅤ{RED}➡ discord.gg/WdmjUSYehW",
                    ["US"] = "Chat, find teammates, and stay updated in our Discord:\nㅤㅤㅤ{RED}➡ discord.gg/WdmjUSYehW",
                    ["UA"] = "Спілкуйся, знаходь тіммейтів та дізнавайся новини в нашому Discord:\nㅤㅤㅤ{RED}➡ discord.gg/WdmjUSYehW",
                    ["PL"] = "Rozmawiaj, znajdź drużynę i bądź na bieżąco na naszym Discordzie:\nㅤㅤㅤ{RED}➡ discord.gg/WdmjUSYehW",
                    ["DE"] = "Chatte, finde Teammates und bleibe informiert auf unserem Discord:\nㅤㅤㅤ{RED}➡ discord.gg/WdmjUSYehW"
                },
                ["reklama_4"] = new() {
                    ["RU"] = "Хотите персональный стиль? Собери сет скинов на\nㅤㅤㅤ{RED}➡ skins.armaturix.net",
                    ["US"] = "Want your own style? Customize your skins at\nㅤㅤㅤ{RED}➡ skins.armaturix.net",
                    ["UA"] = "Хочеш власний стиль? Створюй свій сет скінів на\nㅤㅤㅤ{RED}➡ skins.armaturix.net",
                    ["PL"] = "Chcesz własny styl? Skonfiguruj swoje skiny na\nㅤㅤㅤ{RED}➡ skins.armaturix.net",
                    ["DE"] = "Dein eigener Stil? Erstelle dein Skin-Set auf\nㅤㅤㅤ{RED}➡ skins.armaturix.net"
                },
                ["reklama_5"] = new() {
                    ["RU"] = "Видел читера? Сообщи о нем командой:\nㅤㅤㅤ{RED}➡ !report",
                    ["US"] = "Saw a cheater? Report them using:\nㅤㅤㅤ{RED}➡ !report",
                    ["UA"] = "Побачив чітера? Повідом командою:\nㅤㅤㅤ{RED}➡ !report",
                    ["PL"] = "Widziałeś cheatera? Zgłoś go za pomocą:\nㅤㅤㅤ{RED}➡ !report",
                    ["DE"] = "Hast du einen Cheater gesehen? Melde ihn mit:\nㅤㅤㅤ{RED}➡ !report"
                },
                ["reklama_6"] = new() {
                    ["RU"] = "Посмотреть список серверов:\nㅤㅤㅤ{RED}➡ !servers",
                    ["US"] = "View the server list:\nㅤㅤㅤ{RED}➡ !servers",
                    ["UA"] = "Переглянути список серверів:\nㅤㅤㅤ{RED}➡ !servers",
                    ["PL"] = "Zobacz listę serwerów:\nㅤㅤㅤ{RED}➡ !servers",
                    ["DE"] = "Serverliste anzeigen:\nㅤㅤㅤ{RED}➡ !servers"
                },
                ["reklama_7"] = new() {
                    ["RU"] = "Нет админов? Голосуй за бан нарушителя:\nㅤㅤㅤ{RED}➡ !voteban\nㅤㅤㅤ{RED}➡ !votemute\nㅤㅤㅤ{RED}➡ !votekick",
                    ["US"] = "No admins online? Vote to ban rule breakers:\nㅤㅤㅤ{RED}➡ !voteban\nㅤㅤㅤ{RED}➡ !votemute\nㅤㅤㅤ{RED}➡ !votekick",
                    ["UA"] = "Немає адмінів? Голосуй за бан порушника:\nㅤㅤㅤ{RED}➡ !voteban\nㅤㅤㅤ{RED}➡ !votemute\nㅤㅤㅤ{RED}➡ !votekick",
                    ["PL"] = "Brak adminów? Zagłosuj za banem dla cheatera:\nㅤㅤㅤ{RED}➡ !voteban\nㅤㅤㅤ{RED}➡ !votemute\nㅤㅤㅤ{RED}➡ !votekick",
                    ["DE"] = "Keine Admins online? Stimme für einen Bann ab:\nㅤㅤㅤ{RED}➡ !voteban\nㅤㅤㅤ{RED}➡ !votemute\nㅤㅤㅤ{RED}➡ !votekick"
                },
                ["reklama_8"] = new() {
                    ["RU"] = "Наши сервера — это стабильность, качество и честная игра!",
                    ["US"] = "Our servers offer stability, quality, and fair play!",
                    ["UA"] = "Наші сервери — це стабільність, якість та чесна гра!",
                    ["PL"] = "Nasze serwery to stabilność, jakość i uczciwa gra!",
                    ["DE"] = "Unsere Server stehen für Stabilität, Qualität und faires Gameplay!"
                },
                ["reklama_9"] = new() {
                    ["RU"] = "Ищешь крутые товары? Используй команду:\nㅤㅤㅤ{LIGHTBLUE}➡ !shop",
                    ["US"] = "Looking for cool items? Use the command:\nㅤㅤㅤ{LIGHTBLUE}➡ !shop",
                    ["UA"] = "Шукаєш круті товари? Використовуй команду:\nㅤㅤㅤ{LIGHTBLUE}➡ !shop",
                    ["PL"] = "Szukasz świetnych przedmiotów? Użyj komendy:\nㅤㅤㅤ{LIGHTBLUE}➡ !shop",
                    ["DE"] = "Suchst du coole Artikel? Nutze den Befehl:\nㅤㅤㅤ{LIGHTBLUE}➡ !shop"
                },
                ["reklama_10"] = new() {
                    ["RU"] = "Опробуй уникальные кастомные скины оружия! \nИспользуй команду:\nㅤㅤㅤ{LIGHTBLUE}➡ !cw",
                    ["US"] = "Try out exclusive custom weapon skins! \nUse the command:\nㅤㅤㅤ{LIGHTBLUE}➡ !cw",
                    ["UA"] = "Спробуй унікальні кастомні скіни зброї! \nВикористовуй команду:\nㅤㅤㅤ{LIGHTBLUE}➡ !cw",
                    ["PL"] = "Wypróbuj unikalne niestandardowe skiny broni! \nUżyj komendy:\nㅤㅤㅤ{LIGHTBLUE}➡ !cw",
                    ["DE"] = "Teste exklusive benutzerdefinierte Waffenskins! \nNutze den Befehl:\nㅤㅤㅤ{LIGHTBLUE}➡ !cw"
                },
                ["prizes_announcement"] = new() {
                    ["RU"] = "{DEFAULT}Топ-игроки месяца получают призы!\nㅤㅤㅤ{MAGENTA}Топ 1 - Vip Premium (30 дней)\nㅤㅤㅤ{SILVER}Топ 2 - Vip Premium (14 дней)\nㅤㅤㅤ{ORANGE}Топ 3 - Vip (30 дней)\nㅤㅤㅤ{RED}Играй и побеждай!",
                    ["US"] = "{DEFAULT}Top players of the month receive rewards!\nㅤㅤㅤ{MAGENTA}Top 1 - Vip Premium (30 days)\nㅤㅤㅤ{SILVER}Top 2 - Vip Premium (14 days)\nㅤㅤㅤ{ORANGE}Top 3 - Vip (30 days)\nㅤㅤㅤ{RED}Play and win!",
                    ["UA"] = "{DEFAULT}Топ-гравці місяця отримують призи!\nㅤㅤㅤ{MAGENTA}Топ 1 - Vip Premium (30 днів)\nㅤㅤㅤ{SILVER}Топ 2 - Vip Premium (14 днів)\nㅤㅤㅤ{ORANGE}Топ 3 - Vip (30 днів)\nㅤㅤㅤ{RED}Грай та перемагай!",
                    ["PL"] = "{DEFAULT}Najlepsi gracze miesiąca otrzymują nagrody!\nㅤㅤㅤ{MAGENTA}Top 1 - Vip Premium (30 dni)\nㅤㅤㅤ{SILVER}Top 2 - Vip Premium (14 dni)\nㅤㅤㅤ{ORANGE}Top 3 - Vip (30 dni)\nㅤㅤㅤ{RED}Graj i wygrywaj!",
                    ["DE"] = "{DEFAULT}Top-Spieler des Monats erhalten Belohnungen!\nㅤㅤㅤ{MAGENTA}Top 1 - Vip Premium (30 Tage)\nㅤㅤㅤ{SILVER}Top 2 - Vip Premium (14 Tage)\nㅤㅤㅤ{ORANGE}Top 3 - Vip (30 Tage)\nㅤㅤㅤ{RED}Spiele und gewinne!"
                }
            },
            MapsName = new System.Collections.Generic.Dictionary<string, string>
            {
                ["awp_lego_2"] = "AWP_LEGO",
                ["de_mirage"] = "MIRAGE_CLASSIC"
            }
        };

        var ads = new Config
        {
            Ads = new System.Collections.Generic.List<Advertisement>
            {
                new Advertisement
                {
                    Interval = 60,
                    Messages = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>>
                    {
                        new() { { "Chat", "{prefix}{reklama_1}" } },
                        new() { { "Chat", "{prefix}{reklama_2}" }, { "Center", "!viptest - FREE!" } },
                        new() { { "Chat", "{prefix}{reklama_3}" }, { "Console", "DS: discord.gg/WdmjUSYehW" } },
                        new() { { "Chat", "{prefix}{reklama_4}" } },
                        new() { { "Chat", "{prefix}{reklama_5}" } },
                        new() { { "Chat", "{prefix}{reklama_6}" } },
                        new() { { "Chat", "{prefix}{reklama_7}" } },
                        new() { { "Chat", "{prefix}{reklama_8}" } },
                        new() { { "Chat", "{prefix}{reklama_9}" } },
                        new() { { "Chat", "{prefix}{reklama_10}" } },
                        new() { { "Chat", "{prefix}{prizes_announcement}" } }
                    }
                }
            }
        };

        var messages = new Config
        {
            JoinMessages = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>
            {
                ["RU"] = new()
                {
                    "{connected} Игрок {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] вылез из {GREEN}{COUNTRY}{DEFAULT}! Салют!",
                    "{connected} Игрок {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] ворвался из {GREEN}{COUNTRY}{DEFAULT}, как царь!",
                    "{connected} Игрок {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] пришел на сервер. Похоже, скучать не придется!",
                    "{connected} Игрок {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] только что упал с неба. Наверное, из {GREEN}{COUNTRY}{DEFAULT}!",
                    "{connected} Игрок {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] вошел в лобби, несет в кармане полный боезапас!",
                    "{connected} Игрок {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] уже тут, и он готов играть!",
                    "{connected} Игрок {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] прибыл… Похоже, долетел из {GREEN}{COUNTRY}{DEFAULT}!",
                    "{connected} Игрок {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] зашел в игру. Где все спрятались?",
                    "{connected} Игрок {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] из {GREEN}{COUNTRY} {DEFAULT}готов к бою!",
                    "{connected} Игрок {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] залетел, как вихрь из {GREEN}{COUNTRY}{DEFAULT}!",
                    "{connected} Игрок {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] вернулся из отпуска. Грядет жара!",
                    "{connected} Игрок {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] наконец-то зашел(a) на сервер...",
                    "{connected} Игрок {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] распаковал чемоданы прямо из {GREEN}{COUNTRY}{DEFAULT}.",
                    "{connected} Игрок {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] только что поставил рекорд по скорости входа!",
                    "{connected} Игрок {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] уже здесь и готов к экшену!!!"
                },
                ["US"] = new()
                {
                    "{connected} Player {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] emerged from {GREEN}{COUNTRY}{DEFAULT}! Greetings!",
                    "{connected} Player {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] rushed in from {GREEN}{COUNTRY}{DEFAULT}, like a boss!",
                    "{connected} Player {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] has joined the server. Looks like we won't get bored!",
                    "{connected} Player {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] just fell from the sky. Probably from {GREEN}{COUNTRY}{DEFAULT}!",
                    "{connected} Player {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] entered the lobby, carrying a full load of ammo!",
                    "{connected} Player {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] is already here and ready to play!",
                    "{connected} Player {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] has arrived... Looks like they flew in from {GREEN}{COUNTRY}{DEFAULT}!",
                    "{connected} Player {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] joined the game. Where is everyone hiding?",
                    "{connected} Player {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] from {GREEN}{COUNTRY} {DEFAULT}is ready for battle!",
                    "{connected} Player {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] stormed in like a whirlwind from {GREEN}{COUNTRY}{DEFAULT}!",
                    "{connected} Player {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] is back from vacation. Things are about to heat up!",
                    "{connected} Player {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] finally joined the server...",
                    "{connected} Player {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] just unpacked their suitcases straight from {GREEN}{COUNTRY}{DEFAULT}.",
                    "{connected} Player {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] just set a record for the fastest login!",
                    "{connected} Player {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] is here and ready for action!!!"
                },
                ["UA"] = new()
                {
                    "{connected} Гравець {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] виліз із {GREEN}{COUNTRY}{DEFAULT}! Вітаємо!",
                    "{connected} Гравець {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] увірвався з {GREEN}{COUNTRY}{DEFAULT}, як бос!",
                    "{connected} Гравець {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] приєднався до сервера. Нудьгувати не доведеться!",
                    "{connected} Гравець {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] щойно впав із неба. Мабуть, із {GREEN}{COUNTRY}{DEFAULT}!",
                    "{connected} Гравець {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] зайшов у лобі, несе повний запас боєприпасів!",
                    "{connected} Гравець {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] уже тут і готовий грати!",
                    "{connected} Гравець {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] прибув… Схоже, долетів із {GREEN}{COUNTRY}{DEFAULT}!",
                    "{connected} Гравець {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] приєднався до гри. Де всі поховалися?",
                    "{connected} Гравець {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] з {GREEN}{COUNTRY} {DEFAULT}готовий до бою!",
                    "{connected} Гравець {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] залетів, як вихор із {GREEN}{COUNTRY}{DEFAULT}!",
                    "{connected} Гравець {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] повернувся з відпустки. Чекаємо на спеку!",
                    "{connected} Гравець {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] нарешті зайшов(-ла) на сервер...",
                    "{connected} Гравець {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] розпакував(-ла) валізи прямо з {GREEN}{COUNTRY}{DEFAULT}.",
                    "{connected} Гравець {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] щойно встановив(-ла) рекорд швидкості входу!",
                    "{connected} Гравець {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] уже тут і готовий до екшену!!!"
                },
                ["PL"] = new()
                {
                    "{connected} Gracz {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] wynurzył się z {GREEN}{COUNTRY}{DEFAULT}! Witamy!",
                    "{connected} Gracz {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] wpadł z {GREEN}{COUNTRY}{DEFAULT}, jak szef!",
                    "{connected} Gracz {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] dołączył do serwera. Nudy nie będzie!",
                    "{connected} Gracz {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] właśnie spadł z nieba. Pewnie z {GREEN}{COUNTRY}{DEFAULT}!",
                    "{connected} Gracz {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] wszedł do lobby, ma pełen zapas amunicji!",
                    "{connected} Gracz {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] już tu jest i gotów do gry!",
                    "{connected} Gracz {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] przybył… Wygląda na to, że doleciał z {GREEN}{COUNTRY}{DEFAULT}!",
                    "{connected} Gracz {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] dołączył do gry. Gdzie się wszyscy pochowali?",
                    "{connected} Gracz {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] z {GREEN}{COUNTRY} {DEFAULT}jest gotów do walki!",
                    "{connected} Gracz {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] wleciał niczym wir z {GREEN}{COUNTRY}{DEFAULT}!",
                    "{connected} Gracz {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] wrócił z urlopu. Szykuje się gorąco!",
                    "{connected} Gracz {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] w końcu wszedł(-a) na serwer...",
                    "{connected} Gracz {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] właśnie wypakował walizki prosto z {GREEN}{COUNTRY}{DEFAULT}.",
                    "{connected} Gracz {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] właśnie pobił rekord prędkości wejścia!",
                    "{connected} Gracz {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] już tu jest i gotów na akcję!!!"
                },
                ["DE"] = new()
                {
                    "{connected} Spieler {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] ist aus {GREEN}{COUNTRY}{DEFAULT} aufgetaucht! Willkommen!",
                    "{connected} Spieler {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] ist aus {GREEN}{COUNTRY}{DEFAULT} hereingestürmt, wie ein König!",
                    "{connected} Spieler {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] ist dem Server beigetreten. Langweilig wird es nicht!",
                    "{connected} Spieler {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] ist gerade vom Himmel gefallen. Wahrscheinlich aus {GREEN}{COUNTRY}{DEFAULT}!",
                    "{connected} Spieler {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] betrat die Lobby mit vollem Munitionsvorrat!",
                    "{connected} Spieler {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] ist schon da und bereit zu spielen!",
                    "{connected} Spieler {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] ist angekommen… Sieht so aus, als käme er aus {GREEN}{COUNTRY}{DEFAULT}!",
                    "{connected} Spieler {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] hat das Spiel betreten. Wo verstecken sich alle?",
                    "{connected} Spieler {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] aus {GREEN}{COUNTRY} {DEFAULT}ist kampfbereit!",
                    "{connected} Spieler {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] stürmte herein wie ein Wirbelwind aus {GREEN}{COUNTRY}{DEFAULT}!",
                    "{connected} Spieler {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] ist aus dem Urlaub zurück. Jetzt wird’s heiß!",
                    "{connected} Spieler {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] ist endlich auf dem Server angekommen...",
                    "{connected} Spieler {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] hat gerade seine Koffer direkt aus {GREEN}{COUNTRY}{DEFAULT} ausgepackt.",
                    "{connected} Spieler {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] hat soeben einen Rekord für den schnellsten Login aufgestellt!",
                    "{connected} Spieler {DEFAULT}[ {LIGHTBLUE}{PLAYERNAME} {DEFAULT}] ist schon da und bereit für Action!!!"
                }
            },
            LeaveMessages = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>
            {
                ["RU"] = new()
                {
                    "{disconnected} {DARKRED}Игрок {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}превратился в пиксели{GREY}…",
                    "{disconnected} {DARKRED}Игрок {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}решил спасти галактику в одиночку{GREY}.",
                    "{disconnected} {DARKRED}Игрок {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}выбыл из игры!",
                    "{disconnected} {DARKRED}Игрок {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}ушел прокачивать скилл оффлайн{GREY}.",
                    "{disconnected} {DARKRED}Игрок {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}ярко вспыхнул и погас{GREY}. {DARKRED}RIP{GREY}.",
                    "{disconnected} {DARKRED}Игрок {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}ушел становиться топ-стримером{GREY}.",
                    "{disconnected} {DARKRED}Игрок {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}выпал из реальности{GREY}.",
                    "{disconnected} {DARKRED}Игрок {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}пошел искать ракетные ботинки{GREY}.",
                    "{disconnected} {DARKRED}Игрок {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}перегрелся и сбежал{GREY}.",
                    "{disconnected} {DARKRED}Игрок {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}перешел в более уютное лобби{GREY}.",
                    "{disconnected} {DARKRED}Игрок {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}поймал баг и отвалился{GREY}.",
                    "{disconnected} {DARKRED}Игрок {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}внезапно вышел без предупреждения!",
                    "{disconnected} {DARKRED}Игрок {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}улетел собирать донаты{GREY}.",
                    "{disconnected} {DARKRED}Игрок {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}забыл ключ от двери{GREY}, {DARKRED}пошел открывать{GREY}.",
                    "{disconnected} {DARKRED}Игрок {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}пропал в поисках печенек{GREY}."
                },
                ["US"] = new()
                {
                    "{disconnected} {DARKRED}Player {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}turned into pixels{GREY}…",
                    "{disconnected} {DARKRED}Player {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}decided to save the galaxy solo{GREY}.",
                    "{disconnected} {DARKRED}Player {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}dropped out of the game{GREY}!",
                    "{disconnected} {DARKRED}Player {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}went to level up offline{GREY}.",
                    "{disconnected} {DARKRED}Player {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}flashed brightly and disappeared{GREY}. {DARKRED}RIP{GREY}.",
                    "{disconnected} {DARKRED}Player {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}went off to become a top streamer{GREY}.",
                    "{disconnected} {DARKRED}Player {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}fell out of reality{GREY}.",
                    "{disconnected} {DARKRED}Player {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}went looking for rocket boots{GREY}.",
                    "{disconnected} {DARKRED}Player {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}overheated and ran away{GREY}.",
                    "{disconnected} {DARKRED}Player {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}switched to a cozier lobby{GREY}.",
                    "{disconnected} {DARKRED}Player {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}caught a bug and crashed{GREY}.",
                    "{disconnected} {DARKRED}Player {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}suddenly left without warning{GREY}!",
                    "{disconnected} {DARKRED}Player {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}flew away to collect donations{GREY}.",
                    "{disconnected} {DARKRED}Player {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}forgot the door key{GREY}, {DARKRED}went to open it{GREY}.",
                    "{disconnected} {DARKRED}Player {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}disappeared in search of cookies{GREY}."
                },
                ["UA"] = new()
                {
                    "{disconnected} {DARKRED}Гравець {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}перетворився на пікселі{GREY}…",
                    "{disconnected} {DARKRED}Гравець {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}вирішив самотужки врятувати галактику{GREY}.",
                    "{disconnected} {DARKRED}Гравець {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}вийшов з гри{GREY}!",
                    "{disconnected} {DARKRED}Гравець {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}пішов прокачувати скіл офлайн{GREY}.",
                    "{disconnected} {DARKRED}Гравець {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}яскраво спалахнув і зник{GREY}. {DARKRED}RIP{GREY}.",
                    "{disconnected} {DARKRED}Гравець {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}пішов ставати топ-стрімером{GREY}.",
                    "{disconnected} {DARKRED}Гравець {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}випав із реальності{GREY}.",
                    "{disconnected} {DARKRED}Гравець {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}пішов шукати ракетні черевики{GREY}.",
                    "{disconnected} {DARKRED}Гравець {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}перегрівся і втік{GREY}.",
                    "{disconnected} {DARKRED}Гравець {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}перейшов у більш затишне лобі{GREY}.",
                    "{disconnected} {DARKRED}Гравець {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}спіймав баг і відвалився{GREY}.",
                    "{disconnected} {DARKRED}Гравець {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}зненацька вийшов без попередження{GREY}!",
                    "{disconnected} {DARKRED}Гравець {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}відлетів збирати донати{GREY}.",
                    "{disconnected} {DARKRED}Гравець {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}забув ключ від дверей{GREY}, {DARKRED}пішов відчиняти{GREY}.",
                    "{disconnected} {DARKRED}Гравець {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}зник у пошуках печива{GREY}."
                },
                ["PL"] = new()
                {
                    "{disconnected} {DARKRED}Gracz {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}zmienił się w piksele{GREY}…",
                    "{disconnected} {DARKRED}Gracz {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}postanowił uratować galaktykę w pojedynkę{GREY}.",
                    "{disconnected} {DARKRED}Gracz {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}odpadł z gry{GREY}!",
                    "{disconnected} {DARKRED}Gracz {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}poszedł trenować umiejętności offline{GREY}.",
                    "{disconnected} {DARKRED}Gracz {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}rozbłysnął i zgasł{GREY}. {DARKRED}RIP{GREY}.",
                    "{disconnected} {DARKRED}Gracz {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}poszedł zostać top streamerem{GREY}.",
                    "{disconnected} {DARKRED}Gracz {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}wypadł z rzeczywistości{GREY}.",
                    "{disconnected} {DARKRED}Gracz {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}poszedł szukać rakietowych butów{GREY}.",
                    "{disconnected} {DARKRED}Gracz {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}przegrzał się i uciekł{GREY}.",
                    "{disconnected} {DARKRED}Gracz {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}przeniósł się do przytulniejszego lobby{GREY}.",
                    "{disconnected} {DARKRED}Gracz {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}złapał buga i się rozłączył{GREY}.",
                    "{disconnected} {DARKRED}Gracz {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}nagle wyszedł bez ostrzeżenia{GREY}!",
                    "{disconnected} {DARKRED}Gracz {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}odleciał zbierać donaty{GREY}.",
                    "{disconnected} {DARKRED}Gracz {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}zapomniał klucza do drzwi{GREY}, {DARKRED}poszedł otworzyć{GREY}.",
                    "{disconnected} {DARKRED}Gracz {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}zniknął w poszukiwaniu ciasteczek{GREY}."
                },
                ["DE"] = new()
                {
                    "{disconnected} {DARKRED}Spieler {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}wurde zu Pixeln{GREY}…",
                    "{disconnected} {DARKRED}Spieler {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}beschloss, die Galaxie im Alleingang zu retten{GREY}.",
                    "{disconnected} {DARKRED}Spieler {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}ist aus dem Spiel ausgeschieden{GREY}!",
                    "{disconnected} {DARKRED}Spieler {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}ging offline seinen Skill trainieren{GREY}.",
                    "{disconnected} {DARKRED}Spieler {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}leuchtete hell auf und verschwand{GREY}. {DARKRED}RIP{GREY}.",
                    "{disconnected} {DARKRED}Spieler {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}machte sich auf, ein Top-Streamer zu werden{GREY}.",
                    "{disconnected} {DARKRED}Spieler {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}ist aus der Realität gefallen{GREY}.",
                    "{disconnected} {DARKRED}Spieler {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}machte sich auf die Suche nach Raketenstiefeln{GREY}.",
                    "{disconnected} {DARKRED}Spieler {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}überhitzte und rannte davon{GREY}.",
                    "{disconnected} {DARKRED}Spieler {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}wechselte in eine gemütlichere Lobby{GREY}.",
                    "{disconnected} {DARKRED}Spieler {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}hat einen Bug erwischt und sich verabschiedet{GREY}.",
                    "{disconnected} {DARKRED}Spieler {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}hat plötzlich ohne Vorwarnung das Spiel verlassen{GREY}!",
                    "{disconnected} {DARKRED}Spieler {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}ist davon geflogen, um Spenden zu sammeln{GREY}.",
                    "{disconnected} {DARKRED}Spieler {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}hat den Schlüssel vergessen und ging ihn holen{GREY}.",
                    "{disconnected} {DARKRED}Spieler {GREY}[ {SILVER}{PLAYERNAME} {GREY}] {DARKRED}ist auf der Suche nach Keksen verschwunden{GREY}."
                }
            }
        };

        var servers = new Config
        {
            TitleAnnounceServers = "{announce_servers}",
            Servers = new ServerInfo
            {
                Enabled = false,
                Interval = 125,
                QueryTimeoutMs = 1000,
                CacheTtlSeconds = 10,
                List = new System.Collections.Generic.List<ServerData>
                {
                    new ServerData
                    {
                        Ip = "127.0.0.1",
                        Port = 27015,
                        MessageTemplate = "{LIGHTBLUE}ㅤㅤㅤ➡{DEFAULT} {GREEN}{SERVER_IP}:{SERVER_PORT}{DEFAULT} - {LIGHTBLUE}{SERVER_MAP}{DEFAULT} | {GREEN}{SERVER_PLAYERS}{DEFAULT}/{GREEN}{SERVER_MAXPLAYERS}",
                        MessageTemplateConsole = "{SERVER_IP}:{SERVER_PORT} - {SERVER_MAP} | {SERVER_PLAYERS}/{SERVER_MAXPLAYERS}",
                        MaxPlayersFallback = 24
                    }
                }
            }
        };

        var settingsPath = Path.Combine(directory, "Settings.json");
        var adsPath = Path.Combine(directory, "Ads.json");
        var messagesPath = Path.Combine(directory, "Messages.json");
        var serversPath = Path.Combine(directory, "Servers.json");

        var writeOptions = new JsonSerializerOptions 
        { 
            WriteIndented = true, 
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, writeOptions));
        File.WriteAllText(adsPath, JsonSerializer.Serialize(ads, writeOptions));
        File.WriteAllText(messagesPath, JsonSerializer.Serialize(messages, writeOptions));
        File.WriteAllText(serversPath, JsonSerializer.Serialize(servers, writeOptions));

        // Создаём README с инструкциями
        CreateConfigReadme(directory);

        _logger.Info($"[NotifyMessages] ✓ Created default configuration files:");
        _logger.Info($"  • Settings.json - Basic settings, localization, welcome messages");
        _logger.Info($"  • Ads.json - Advertisement messages (interval: 60s)");
        _logger.Info($"  • Messages.json - Join/Leave messages with GeoIP support");
        _logger.Info($"  • Servers.json - Server monitoring (disabled by default)");
        _logger.Info($"  • README.txt - Configuration guide");
        _logger.Info($"Directory: {directory}");
        _logger.Info("Use 'css_advert_reload' command to reload configs after editing.");

        return MergeParts(settings, ads, messages, servers);
    }

    private void ValidateConfig(Config config, string directory)
    {
        var warnings = new System.Collections.Generic.List<string>();
        var info = new System.Collections.Generic.List<string>();

        // Проверка базовых настроек
        if (string.IsNullOrEmpty(config.DefaultLang))
        {
            warnings.Add("DefaultLang is not set. Using 'RU' as fallback.");
        }

        // Проверка WelcomeMessage
        if (config.WelcomeMessage != null)
        {
            if (string.IsNullOrEmpty(config.WelcomeMessage.Message))
            {
                warnings.Add("WelcomeMessage.Message is empty. Players won't see welcome message.");
            }
            if (config.WelcomeMessage.DisplayDelay < 0 || config.WelcomeMessage.DisplayDelay > 60)
            {
                warnings.Add($"WelcomeMessage.DisplayDelay ({config.WelcomeMessage.DisplayDelay}s) is unusual. Recommended: 1-10 seconds.");
            }
        }

        // Проверка рекламы
        if (config.Ads != null && config.Ads.Count > 0)
        {
            info.Add($"✓ Loaded {config.Ads.Count} advertisement block(s)");
            foreach (var ad in config.Ads)
            {
                if (ad.Interval < 1)
                {
                    warnings.Add($"Ad block has Interval < 1 second. This will be clamped to 1 second.");
                }
                if (ad.Messages == null || ad.Messages.Count == 0)
                {
                    warnings.Add("Ad block has no messages. It will be skipped.");
                }
            }
        }
        else
        {
            info.Add("ℹ No advertisement blocks configured (Ads.json is empty)");
        }

        // Проверка сообщений входа/выхода
        if (config.JoinMessages != null && config.JoinMessages.Count > 0)
        {
            info.Add($"✓ Loaded JoinMessages for {config.JoinMessages.Count} language(s)");
        }
        else
        {
            info.Add("ℹ No JoinMessages configured. Players won't see join notifications.");
        }

        if (config.LeaveMessages != null && config.LeaveMessages.Count > 0)
        {
            info.Add($"✓ Loaded LeaveMessages for {config.LeaveMessages.Count} language(s)");
        }
        else
        {
            info.Add("ℹ No LeaveMessages configured. Players won't see leave notifications.");
        }

        // Проверка серверов
        if (config.Servers != null)
        {
            if (config.Servers.Enabled)
            {
                if (config.Servers.List == null || config.Servers.List.Count == 0)
                {
                    warnings.Add("Server monitoring is ENABLED but no servers configured!");
                }
                else
                {
                    info.Add($"✓ Server monitoring ENABLED for {config.Servers.List.Count} server(s)");
                    
                    if (config.Servers.Interval < 5)
                    {
                        warnings.Add($"Servers.Interval ({config.Servers.Interval}s) is too low. Recommended: >= 5 seconds.");
                    }
                    
                    if (config.Servers.QueryTimeoutMs < 200 || config.Servers.QueryTimeoutMs > 5000)
                    {
                        warnings.Add($"Servers.QueryTimeoutMs ({config.Servers.QueryTimeoutMs}ms) is unusual. Recommended: 500-2000ms.");
                    }

                    // Проверка каждого сервера
                    for (int i = 0; i < config.Servers.List.Count; i++)
                    {
                        var server = config.Servers.List[i];
                        if (string.IsNullOrEmpty(server.Ip))
                        {
                            warnings.Add($"Server #{i + 1}: IP is empty!");
                        }
                        if (server.Port <= 0 || server.Port > 65535)
                        {
                            warnings.Add($"Server #{i + 1}: Invalid port {server.Port}");
                        }
                        if (server.Ip == "127.0.0.1" && server.Port == 27015)
                        {
                            info.Add($"ℹ Server #{i + 1}: Using example values (127.0.0.1:27015). Don't forget to change!");
                        }
                    }
                }
            }
            else
            {
                info.Add("ℹ Server monitoring is DISABLED (Servers.Enabled = false)");
            }
        }

        // Проверка локализации
        if (config.LanguageMessages != null && config.LanguageMessages.Count > 0)
        {
            info.Add($"✓ Loaded {config.LanguageMessages.Count} localization tag(s)");
        }
        else
        {
            warnings.Add("No LanguageMessages configured. Localization tags won't work!");
        }

        // Вывод результатов
        if (info.Count > 0)
        {
            _logger.Info("═══ Configuration Loaded ═══");
            foreach (var msg in info)
            {
                _logger.Info(msg);
            }
        }

        if (warnings.Count > 0)
        {
            _logger.Info("⚠ Configuration Warnings:");
            foreach (var msg in warnings)
            {
                _logger.Info($"  ⚠ {msg}");
            }
            _logger.Info($"Configuration directory: {directory}");
        }
    }

    private void CreateConfigReadme(string directory)
    {
        var readmePath = Path.Combine(directory, "README.txt");
        var content = @"═══════════════════════════════════════════════════════════════════════════════
    NotifyMessages Plugin - Configuration Guide
═══════════════════════════════════════════════════════════════════════════════

This folder contains 4 configuration files:

┌─────────────────────────────────────────────────────────────────────────────┐
│ 1. Settings.json - Main Settings                                           │
├─────────────────────────────────────────────────────────────────────────────┤
│ • Debug: Enable detailed logging (true/false)                              │
│ • PrintToCenterHtml: Use HTML for center messages (true/false)             │
│ • HtmlCenterDuration: How long HTML center shows (seconds)                 │
│ • ShowHtmlWhenDead: Show HTML to dead players (true/false)                 │
│ • DefaultLang: Default language (RU/US/UA/PL/DE)                           │
│                                                                             │
│ • WelcomeMessage: Player welcome message                                   │
│   - MessageType: 0=Chat, 1=Center, 2=CenterHtml, 3=Console                │
│   - Message: Text with placeholders                                        │
│   - DisplayDelay: Delay before showing (seconds)                           │
│                                                                             │
│ • LanguageMessages: Translations for all languages                         │
│   Format: ""tag"": { ""RU"": ""текст"", ""US"": ""text"", ... }              │
│                                                                             │
│ • MapsName: Replace map names                                              │
│   Example: ""de_dust2"": ""DUST 2""                                          │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│ 2. Ads.json - Advertisement Messages                                       │
├─────────────────────────────────────────────────────────────────────────────┤
│ • Ads: Array of advertisement blocks                                       │
│   - Interval: Show every N seconds (minimum 1)                             │
│   - Messages: Array of messages (rotate automatically)                     │
│                                                                             │
│ Each message can have multiple channels:                                   │
│   { ""Chat"": ""text"" }                    - Show in chat                   │
│   { ""Center"": ""text"" }                  - Show in center                │
│   { ""Console"": ""text"" }                 - Show in console               │
│   { ""Chat"": ""..."", ""Center"": ""..."" }  - Show in both!                │
│                                                                             │
│ Use {tags} from LanguageMessages for localization!                         │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│ 3. Messages.json - Join/Leave Messages                                     │
├─────────────────────────────────────────────────────────────────────────────┤
│ • JoinMessages: Messages when player connects                              │
│ • LeaveMessages: Messages when player disconnects                          │
│                                                                             │
│ Format: { ""RU"": [""msg1"", ""msg2"", ...], ""US"": [...] }                  │
│                                                                             │
│ Random message is picked from array for variety!                           │
│                                                                             │
│ Available placeholders:                                                    │
│   {PLAYERNAME} - Player's name                                             │
│   {COUNTRY}    - Country (detected by GeoIP)                               │
│   {CITY}       - City (detected by GeoIP)                                  │
│   {connected}  - Localized ""Connected"" prefix                             │
│   {disconnected} - Localized ""Disconnected"" prefix                        │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│ 4. Servers.json - Server Monitoring                                        │
├─────────────────────────────────────────────────────────────────────────────┤
│ • TitleAnnounceServers: Title for server list                              │
│ • Servers:                                                                 │
│   - Enabled: Enable server monitoring (true/false)                         │
│   - Interval: Query every N seconds (minimum 5)                            │
│   - QueryTimeoutMs: Timeout for A2S query (200-5000 ms)                   │
│   - CacheTtlSeconds: Cache lifetime (0-60 seconds)                         │
│   - List: Array of servers to monitor                                      │
│                                                                             │
│ Each server:                                                               │
│   - Ip: IP address or hostname                                             │
│   - Port: Server port                                                      │
│   - MessageTemplate: Chat message template                                 │
│   - MessageTemplateConsole: Console message template                       │
│   - MaxPlayersFallback: Max players if server is offline                   │
│                                                                             │
│ Available placeholders:                                                    │
│   {SERVER_IP}         - Server IP                                          │
│   {SERVER_PORT}       - Server port                                        │
│   {SERVER_MAP}        - Current map (or ""OFFLINE"")                        │
│   {SERVER_PLAYERS}    - Current players                                    │
│   {SERVER_MAXPLAYERS} - Maximum players                                    │
│                                                                             │
│ NOTE: Monitoring is DISABLED by default. Set Enabled: true to activate!   │
└─────────────────────────────────────────────────────────────────────────────┘

═══════════════════════════════════════════════════════════════════════════════
    Color Tags (use in any message)
═══════════════════════════════════════════════════════════════════════════════

{DEFAULT} or {WHITE}  - White/default       {RED}            - Red
{GREEN}  or {LIME}    - Green               {BLUE}           - Blue
{YELLOW} or {GOLD}    - Yellow              {ORANGE}         - Orange
{LIGHTBLUE}           - Light blue          {DARKBLUE}       - Dark blue
{PURPLE} or {MAGENTA} - Purple              {LIGHTPURPLE}    - Light purple
{GREY}   or {GRAY}    - Grey                {SILVER}         - Silver
{DARKRED}             - Dark red            {OLIVE}          - Olive
{LIGHTYELLOW}         - Light yellow        {BLUEGREY}       - Blue-grey

{SPACE}               - Wide space for alignment
\n                    - New line

═══════════════════════════════════════════════════════════════════════════════
    System Placeholders (available everywhere)
═══════════════════════════════════════════════════════════════════════════════

{MAP}        - Current map name (or custom name from MapsName)
{TIME}       - Current time (HH:mm:ss)
{DATE}       - Current date (dd.MM.yyyy)
{SERVERNAME} - Server hostname
{IP}         - Server IP
{PORT}       - Server port
{MAXPLAYERS} - Max player slots
{PLAYERS}    - Current online players

═══════════════════════════════════════════════════════════════════════════════
    Commands
═══════════════════════════════════════════════════════════════════════════════

css_servers                - Show server list (for players)
css_announce_restart <sec> - Announce restart in N seconds (1-3600)
css_announce_update <sec>  - Announce update in N seconds (1-3600)
css_advert_reload          - Reload all configs (requires @css/root)

═══════════════════════════════════════════════════════════════════════════════
    Tips
═══════════════════════════════════════════════════════════════════════════════

✓ All queries run ASYNCHRONOUSLY - no server lag!
✓ Messages are CACHED by language - better performance!
✓ Use 'css_advert_reload' after editing - no restart needed!
✓ GeoIP auto-detects player's country for language selection
✓ Server monitoring uses A2S protocol (Source engine standard)
✓ HTML center messages support rich formatting

═══════════════════════════════════════════════════════════════════════════════

Version: 2.0.0
Author: Armatura
More info: Check README.md in plugin folder

═══════════════════════════════════════════════════════════════════════════════
";

        try
        {
            File.WriteAllText(readmePath, content, System.Text.Encoding.UTF8);
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to create README.txt", ex);
        }
    }
}
