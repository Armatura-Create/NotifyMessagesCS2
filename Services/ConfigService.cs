using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Collections.Generic;

namespace NotifyMessages;

/// Сервис работы с конфигурацией плагина: загрузка/сохранение и создание дефолтных файлов
public sealed class ConfigService
{
    private readonly ILogger _logger;

    public ConfigService(ILogger logger)
    {
        _logger = logger;
    }

    // ---- Public API -----------------------------------------------------------

    /// Загружает конфигурацию из 4 файлов и объединяет в единый Config
    public Config LoadOrCreate(string rootDirectory)
    {
        var directory = Path.Combine(rootDirectory, "configs/plugins/NotifyMessages");
        Directory.CreateDirectory(directory);

        var settingsPath = Path.Combine(directory, "Settings.json");
        var messagesPath = Path.Combine(directory, "Messages.json");
        var adsPath = Path.Combine(directory, "Ads.json");
        var serversPath = Path.Combine(directory, "Servers.json");

        // Проверяем наличие хотя бы одного файла
        var filesExist = File.Exists(settingsPath) || File.Exists(messagesPath) || 
                         File.Exists(adsPath) || File.Exists(serversPath);

        if (!filesExist)
        {
            _logger.Info("═══════════════════════════════════════════════════════════════");
            _logger.Info("  NotifyMessages - First Run Detected!");
            _logger.Info("  Creating default configuration files...");
            _logger.Info("═══════════════════════════════════════════════════════════════");
            
            return CreateDefaultConfigs(directory);
        }

        // Загружаем каждый файл отдельно
        var settings = LoadSettings(settingsPath);
        var messages = LoadMessages(messagesPath);
        var ads = LoadAds(adsPath);
        var servers = LoadServers(serversPath);

        var config = MergeParts(settings, messages, ads, servers);
        
        // Валидация и предупреждения
        ValidateConfig(config, directory);
        
        return config;
    }

    // ---- Загрузка отдельных файлов --------------------------------------------

    private SettingsConfig? LoadSettings(string path)
    {
        if (!File.Exists(path))
        {
            _logger.Info($"[Config] Settings.json not found, using defaults");
            return null;
        }
        
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SettingsConfig>(json,
                new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip });
        }
        catch (Exception ex)
        {
            _logger.Error($"[Config] Failed to load Settings.json", ex);
            return null;
        }
    }

    private MessagesConfig? LoadMessages(string path)
    {
        if (!File.Exists(path))
        {
            _logger.Info($"[Config] Messages.json not found");
            return null;
        }
        
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<MessagesConfig>(json,
                new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip });
        }
        catch (Exception ex)
        {
            _logger.Error($"[Config] Failed to load Messages.json", ex);
            return null;
        }
    }

    private AdsConfig? LoadAds(string path)
    {
        if (!File.Exists(path))
        {
            _logger.Info($"[Config] Ads.json not found");
            return null;
        }
        
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AdsConfig>(json,
                new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip });
        }
        catch (Exception ex)
        {
            _logger.Error($"[Config] Failed to load Ads.json", ex);
            return null;
        }
    }

    private ServersConfig? LoadServers(string path)
    {
        if (!File.Exists(path))
        {
            _logger.Info($"[Config] Servers.json not found");
            return null;
        }
        
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ServersConfig>(json,
                new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip });
        }
        catch (Exception ex)
        {
            _logger.Error($"[Config] Failed to load Servers.json", ex);
            return null;
        }
    }

    // ---- Объединение частей ---------------------------------------------------

    private Config MergeParts(SettingsConfig? settings, MessagesConfig? messages, AdsConfig? ads, ServersConfig? servers)
    {
        return new Config
        {
            // Из Settings.json
            Debug = settings?.Debug ?? true,
            DefaultLang = settings?.DefaultLang ?? "RU",
            PrintToCenterHtml = settings?.PrintToCenterHtml,
            ShowHtmlWhenDead = settings?.ShowHtmlWhenDead,
            HtmlCenterDuration = settings?.HtmlCenterDuration,
            WelcomeMessage = settings?.WelcomeMessage,
            RestartMessage = settings?.RestartMessage,
            UpdateMessage = settings?.UpdateMessage,
            ChangeTeamMessage = settings?.ChangeTeamMessage,
            JoinTeamMessage = settings?.JoinTeamMessage,
            TitleAnnounceServers = settings?.TitleAnnounceServers,
            MapsName = settings?.MapsName,

            // Из Messages.json
            LanguageMessages = messages?.LanguageMessages,
            JoinMessages = messages?.JoinMessages,
            LeaveMessages = messages?.LeaveMessages,

            // Из Ads.json
            Ads = ads?.Ads,

            // Из Servers.json
            Servers = servers != null ? new ServerInfo
            {
                Enabled = servers.Enabled,
                Interval = servers.Interval,
                QueryTimeoutMs = servers.QueryTimeoutMs,
                CacheTtlSeconds = servers.CacheTtlSeconds,
                List = servers.List ?? new List<ServerData>()
            } : null
        };
    }

    // ---- Создание дефолтных конфигов ------------------------------------------

    private Config CreateDefaultConfigs(string directory)
    {
        var settings = CreateDefaultSettings();
        var messages = CreateDefaultMessages();
        var ads = CreateDefaultAds();
        var servers = CreateDefaultServers();

        // Сохраняем каждый файл
        SaveConfig(Path.Combine(directory, "Settings.json"), settings);
        SaveConfig(Path.Combine(directory, "Messages.json"), messages);
        SaveConfig(Path.Combine(directory, "Ads.json"), ads);
        SaveConfig(Path.Combine(directory, "Servers.json"), servers);

        // Создаём README
        CreateConfigReadme(directory);

        _logger.Info("✓ Settings.json created");
        _logger.Info("✓ Messages.json created");
        _logger.Info("✓ Ads.json created");
        _logger.Info("✓ Servers.json created");
        _logger.Info("✓ README.txt created");
        _logger.Info("═══════════════════════════════════════════════════════════════");
        
        return MergeParts(settings, messages, ads, servers);
    }

    private void SaveConfig<T>(string path, T config)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        
        var json = JsonSerializer.Serialize(config, options);
        File.WriteAllText(path, json, Encoding.UTF8);
    }

    // ---- Дефолтные значения ---------------------------------------------------

    private SettingsConfig CreateDefaultSettings()
    {
        return new SettingsConfig
        {
            Debug = true,
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
            
            RestartMessage = "{prefix}{RED}{will_restarted}",
            UpdateMessage = "{prefix}{RED}{will_updated}",
            ChangeTeamMessage = "{prefix}{changeTeamMessage}",
            JoinTeamMessage = "{prefix}{joinTeamMessage}",
            TitleAnnounceServers = "{prefix}{announce_servers}",
            
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

    private MessagesConfig CreateDefaultMessages()
    {
        return new MessagesConfig
        {
            LanguageMessages = new Dictionary<string, Dictionary<string, string>>
            {
                ["prefix"] = new()
                {
                    ["RU"] = "{LIGHTBLUE}Armaturix ➡{DEFAULT} ",
                    ["US"] = "{LIGHTBLUE}Armaturix ➡{DEFAULT} ",
                    ["UA"] = "{LIGHTBLUE}Armaturix ➡{DEFAULT} ",
                    ["PL"] = "{LIGHTBLUE}Armaturix ➡{DEFAULT} ",
                    ["DE"] = "{LIGHTBLUE}Armaturix ➡{DEFAULT} "
                },
                
                // Системные сообщения
                ["will_restarted"] = new()
                {
                    ["RU"] = "Сервер будет перезапущен через {TIME_RESTART}!",
                    ["US"] = "The server will be restarted in {TIME_RESTART}!",
                    ["UA"] = "Сервер буде перезапущено через {TIME_RESTART}!",
                    ["PL"] = "Serwer zostanie ponownie uruchomiony za {TIME_RESTART}!",
                    ["DE"] = "Der Server wird in {TIME_RESTART} neu gestartet!"
                },
                ["will_updated"] = new()
                {
                    ["RU"] = "Вышло обновление! Сервер будет перезапущен через {TIME_RESTART}.",
                    ["US"] = "An update has been released! The server will restart in {TIME_RESTART}.",
                    ["UA"] = "Вийшло оновлення! Сервер буде перезапущено через {TIME_RESTART}.",
                    ["PL"] = "Wydano aktualizację! Serwer zostanie ponownie uruchomiony za {TIME_RESTART}.",
                    ["DE"] = "Ein Update wurde veröffentlicht! Der Server wird in {TIME_RESTART} neu gestartet."
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
                    ["RU"] = "на игровой сервер {RED}Armaturix",
                    ["US"] = "to the game server {RED}Armaturix",
                    ["UA"] = "на ігровий сервер {RED}Armaturix",
                    ["PL"] = "na serwer gry {RED}Armaturix",
                    ["DE"] = "auf den Spieleserver {RED}Armaturix"
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
                    ["RU"] = "Общайся, находи тиммейтов и узнавай новости в нашем Discord:\nㅤㅤㅤ{RED}➡ discord.gg/WdmjUSYehW",
                    ["US"] = "Chat, find teammates, and stay updated in our Discord:\nㅤㅤㅤ{RED}➡ discord.gg/WdmjUSYehW",
                    ["UA"] = "Спілкуйся, знаходь тіммейтів та дізнавайся новини в нашому Discord:\nㅤㅤㅤ{RED}➡ discord.gg/WdmjUSYehW",
                    ["PL"] = "Rozmawiaj, znajdź drużynę i bądź na bieżąco na naszym Discordzie:\nㅤㅤㅤ{RED}➡ discord.gg/WdmjUSYehW",
                    ["DE"] = "Chatte, finde Teammates und bleibe informiert auf unserem Discord:\nㅤㅤㅤ{RED}➡ discord.gg/WdmjUSYehW"
                },
                ["reklama_4"] = new()
                {
                    ["RU"] = "Хотите персональный стиль? Собери сет скинов на\nㅤㅤㅤ{RED}➡ skins.armaturix.net",
                    ["US"] = "Want your own style? Customize your skins at\nㅤㅤㅤ{RED}➡ skins.armaturix.net",
                    ["UA"] = "Хочеш власний стиль? Створюй свій сет скінів на\nㅤㅤㅤ{RED}➡ skins.armaturix.net",
                    ["PL"] = "Chcesz własny styl? Skonfiguruj swoje skiny na\nㅤㅤㅤ{RED}➡ skins.armaturix.net",
                    ["DE"] = "Dein eigener Stil? Erstelle dein Skin-Set auf\nㅤㅤㅤ{RED}➡ skins.armaturix.net"
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

    private AdsConfig CreateDefaultAds()
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

    private ServersConfig CreateDefaultServers()
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

    private void CreateConfigReadme(string directory)
    {
        var readmePath = Path.Combine(directory, "README.txt");
        var content = @"═══════════════════════════════════════════════════════════════════════════════
  NotifyMessages - Configuration Guide
═══════════════════════════════════════════════════════════════════════════════

Конфигурация плагина разделена на 4 файла для удобства:

📁 Settings.json   - Основные настройки плагина
📁 Messages.json   - Все переводы и текстовые сообщения
📁 Ads.json        - Настройка рекламных объявлений
📁 Servers.json    - Список серверов для отображения онлайна

═══════════════════════════════════════════════════════════════════════════════
1️⃣  Settings.json - Основные настройки
═══════════════════════════════════════════════════════════════════════════════

• Debug                  - Включить детальное логирование (true/false)
• DefaultLang            - Язык по умолчанию (RU, US, UA, PL, DE)
• PrintToCenterHtml      - Использовать HTML для центральных сообщений
• ShowHtmlWhenDead       - Показывать HTML-сообщения мертвым игрокам
• HtmlCenterDuration     - Длительность показа HTML в секундах
• WelcomeMessage         - Приветственное сообщение при подключении
• RestartMessage         - Сообщение о перезапуске сервера
• UpdateMessage          - Сообщение об обновлении
• ChangeTeamMessage      - Сообщение при смене команды
• JoinTeamMessage        - Сообщение при входе в команду
• TitleAnnounceServers   - Заголовок для команды !servers
• MapsName               - Красивые названия карт

═══════════════════════════════════════════════════════════════════════════════
2️⃣  Messages.json - Переводы и сообщения
═══════════════════════════════════════════════════════════════════════════════

Структура:
{
  ""LanguageMessages"": {
    ""ключ"": {
      ""RU"": ""Русский текст"",
      ""US"": ""English text"",
      ""UA"": ""Український текст"",
      ...
    }
  },
  ""JoinMessages"": { ... },   // Сообщения при подключении
  ""LeaveMessages"": { ... }   // Сообщения при отключении
}

Использование в конфигах:
• В Settings.json: ""{prefix}{welcome_player}""
• В Ads.json: ""{prefix}{reklama_1}""

Плагин автоматически подставит перевод для языка игрока!

═══════════════════════════════════════════════════════════════════════════════
3️⃣  Ads.json - Реклама
═══════════════════════════════════════════════════════════════════════════════

Каждый блок рекламы:
{
  ""Interval"": 120,           // Интервал показа в секундах
  ""Messages"": [
    { ""Chat"": ""..."" },      // Показать в чате
    { ""Center"": ""..."" },    // Показать в центре экрана
    { ""Console"": ""..."" }    // Показать в консоли
  ]
}

Можно использовать ключи из Messages.json:
{ ""Chat"": ""{prefix}{reklama_1}"" }

═══════════════════════════════════════════════════════════════════════════════
4️⃣  Servers.json - Серверы
═══════════════════════════════════════════════════════════════════════════════

• Enabled              - Включить функционал серверов (true/false)
• Interval             - Интервал опроса в секундах (60+)
• QueryTimeoutMs       - Таймаут A2S запроса в мс (200-5000)
• CacheTtlSeconds      - Время жизни кеша в секундах (0-60)
• List                 - Список серверов

Для каждого сервера:
{
  ""Ip"": ""123.45.67.89"",
  ""Port"": 27015,
  ""MessageTemplate"": ""..."",         // Шаблон для чата
  ""MessageTemplateConsole"": ""..."",  // Шаблон для консоли
  ""MaxPlayersFallback"": 32           // Макс. игроков если сервер OFFLINE
}

Плейсхолдеры для шаблонов:
{SERVER_IP}         - IP сервера
{SERVER_PORT}       - Порт сервера
{SERVER_MAP}        - Текущая карта (или ""OFFLINE"")
{SERVER_PLAYERS}    - Количество игроков
{SERVER_MAXPLAYERS} - Максимум игроков

═══════════════════════════════════════════════════════════════════════════════
🎨 Цветовые теги
═══════════════════════════════════════════════════════════════════════════════

{DEFAULT}     - Цвет по умолчанию
{WHITE}       - Белый
{DARKRED}     - Темно-красный
{GREEN}       - Зеленый
{LIME}        - Лайм (светло-зеленый)
{LIGHTYELLOW} - Светло-желтый
{YELLOW}      - Желтый / Золотой
{GOLD}        - Золотой
{SILVER}      - Серебряный
{GREY}        - Серый
{BLUE}        - Синий
{DARKBLUE}    - Темно-синий
{BLUEGREY}    - Серо-синий
{MAGENTA}     - Пурпурный
{LIGHTRED}    - Светло-красный
{RED}         - Красный
{ORANGE}      - Оранжевый
{LIGHTBLUE}   - Светло-синий

═══════════════════════════════════════════════════════════════════════════════
🔧 Системные плейсхолдеры
═══════════════════════════════════════════════════════════════════════════════

{MAP}           - Название текущей карты
{TIME}          - Текущее время (HH:mm:ss)
{DATE}          - Текущая дата (dd.MM.yyyy)
{SERVERNAME}    - Имя сервера (hostname)
{IP}            - IP сервера
{PORT}          - Порт сервера
{MAXPLAYERS}    - Максимум слотов
{PLAYERS}       - Количество игроков онлайн
{PLAYERNAME}    - Имя игрока (в определённых контекстах)

Специальные (для команд/событий):
{TEAM}          - Название команды
{OLD_TEAM}      - Прежняя команда
{TIME_RESTART}  - Время до рестарта (для команд !restart/!update)

═══════════════════════════════════════════════════════════════════════════════
📝 Примеры использования
═══════════════════════════════════════════════════════════════════════════════

1. Добавить новое рекламное сообщение:

В Messages.json:
""my_custom_ad"": {
  ""RU"": ""Мой текст на русском"",
  ""US"": ""My text in English""
}

В Ads.json:
{
  ""Interval"": 300,
  ""Messages"": [
    { ""Chat"": ""{prefix}{my_custom_ad}"" }
  ]
}

2. Настроить приветствие:

В Settings.json:
""WelcomeMessage"": {
  ""MessageType"": 0,  // 0=Chat, 1=Center, 2=CenterHtml, 3=Console
  ""Message"": ""{prefix}{GREEN}{PLAYERNAME}{DEFAULT}, добро пожаловать!"",
  ""DisplayDelay"": 5
}

3. Добавить сервер для отображения:

В Servers.json добавьте в ""List"":
{
  ""Ip"": ""192.168.1.100"",
  ""Port"": 27015,
  ""MessageTemplate"": ""{LIGHTBLUE}[МОЙ СЕРВЕР]{DEFAULT} {SERVER_MAP} | {GREEN}{SERVER_PLAYERS}{DEFAULT}/{SERVER_MAXPLAYERS}"",
  ""MessageTemplateConsole"": ""Мой Сервер: {SERVER_IP}:{SERVER_PORT}"",
  ""MaxPlayersFallback"": 32
}

═══════════════════════════════════════════════════════════════════════════════
💡 Советы
═══════════════════════════════════════════════════════════════════════════════

1. Перезагрузка конфигов: используйте команду `css_reload_advert` в консоли
2. Проверка онлайна серверов: команда `!servers` или `css_announce_servers`
3. Тестирование команд рестарта: `css_announce_restart <секунды>`
4. Все переводы в одном месте (Messages.json) - легко поддерживать!
5. Включите Debug=true для просмотра детальных логов в консоли

═══════════════════════════════════════════════════════════════════════════════
";

        File.WriteAllText(readmePath, content, Encoding.UTF8);
    }

    // ---- Валидация ------------------------------------------------------------

    private void ValidateConfig(Config config, string directory)
    {
        var warnings = new List<string>();
        var info = new List<string>();

        // Проверка Settings
        if (string.IsNullOrEmpty(config.DefaultLang))
            warnings.Add("DefaultLang not set, using 'RU' as default");

        // Проверка Messages
        if (config.LanguageMessages == null || config.LanguageMessages.Count == 0)
            warnings.Add("No LanguageMessages found in Messages.json");

        // Проверка Ads
        if (config.Ads != null && config.Ads.Count > 0)
            info.Add($"Loaded {config.Ads.Count} advertisement block(s)");

        // Проверка Servers
        if (config.Servers?.Enabled == true)
        {
            if (config.Servers.List == null || config.Servers.List.Count == 0)
                warnings.Add("Servers enabled but List is empty");
            else
                info.Add($"Loaded {config.Servers.List.Count} server(s) for status checking");
        }

        // Вывод предупреждений и информации
        if (warnings.Any())
        {
            _logger.Info("⚠ Configuration Warnings:");
            foreach (var warning in warnings)
                _logger.Info($"  ⚠ {warning}");
        }

        if (info.Any())
        {
            foreach (var i in info)
                _logger.Info($"  ℹ {i}");
        }

        _logger.Info($"✓ Configuration loaded from: {directory}");
    }
}
