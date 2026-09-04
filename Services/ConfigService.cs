using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Globalization;
using System.Collections.Generic;

namespace NotifyMessages;

/// Сервис работы с конфигурацией плагина: загрузка/сохранение и создание дефолтных файлов
public sealed partial class ConfigService
{
    // JsonSerializerOptions дорогие в создании и потокобезопасны — держим по одному экземпляру
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ILogger _logger;

    // Файлы, которые не удалось прочитать в этом заходе — чтобы громко сказать об этом в конце
    private readonly List<string> _failedFiles = new();

    public ConfigService(ILogger logger)
    {
        _logger = logger;
    }

    // ---- Public API -----------------------------------------------------------

    /// Загружает конфигурацию из 4 файлов и объединяет в единый Config
    public Config LoadOrCreate(string rootDirectory)
    {
        _failedFiles.Clear();

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

    private SettingsConfig? LoadSettings(string path) => LoadPart<SettingsConfig>(path, "Settings.json");
    private MessagesConfig? LoadMessages(string path) => LoadPart<MessagesConfig>(path, "Messages.json");
    private AdsConfig? LoadAds(string path) => LoadPart<AdsConfig>(path, "Ads.json");
    private ServersConfig? LoadServers(string path) => LoadPart<ServersConfig>(path, "Servers.json");

    /// Читает одну часть конфига. Любая проблема с файлом — не повод падать:
    /// возвращаем null, а MergeParts подставит значения по умолчанию.
    /// Сообщение об ошибке обязано говорить, ЧТО и ГДЕ чинить: путь, строка, позиция.
    private T? LoadPart<T>(string path, string fileName) where T : class
    {
        if (!File.Exists(path))
        {
            _logger.Info($"[Config] {fileName} не найден — используются значения по умолчанию");
            return null;
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            _logger.Error($"[Config] {fileName}: не удалось прочитать файл {path}. " +
                          "Проверьте права доступа. Используются значения по умолчанию", ex);
            _failedFiles.Add(fileName);
            return null;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            _logger.Error($"[Config] {fileName} пуст ({path}). Используются значения по умолчанию");
            _failedFiles.Add(fileName);
            return null;
        }

        try
        {
            var result = JsonSerializer.Deserialize<T>(json, ReadOptions);
            if (result == null)
            {
                _logger.Error($"[Config] {fileName}: файл содержит null вместо объекта ({path}). " +
                              "Используются значения по умолчанию");
                _failedFiles.Add(fileName);
            }

            return result;
        }
        catch (JsonException ex)
        {
            // LineNumber нумеруется с нуля — приводим к привычному виду
            var line = ex.LineNumber.HasValue ? (ex.LineNumber.Value + 1).ToString(CultureInfo.InvariantCulture) : "?";
            var pos = ex.BytePositionInLine?.ToString(CultureInfo.InvariantCulture) ?? "?";

            _logger.Error($"[Config] {fileName}: ошибка в JSON — строка {line}, позиция {pos}. " +
                          $"Файл: {path}. Весь файл проигнорирован, используются значения по умолчанию. " +
                          $"Проверьте синтаксис (лишняя/пропущенная запятая, кавычки, скобки). Подробности: {ex.Message}");
            _failedFiles.Add(fileName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error($"[Config] {fileName}: не удалось разобрать {path}. " +
                          "Используются значения по умолчанию", ex);
            _failedFiles.Add(fileName);
            return null;
        }
    }

    // ---- Объединение частей ---------------------------------------------------

    private static Config MergeParts(SettingsConfig? settings, MessagesConfig? messages, AdsConfig? ads, ServersConfig? servers)
    {
        return new Config
        {
            // Из Settings.json
            Debug = settings?.Debug ?? false,
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
            RestartNotify = settings?.RestartNotify,
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

    private static void SaveConfig<T>(string path, T config)
    {
        var json = JsonSerializer.Serialize(config, WriteOptions);
        File.WriteAllText(path, json, Encoding.UTF8);
    }

    // ---- Дефолтные значения ---------------------------------------------------

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

        if (_failedFiles.Count > 0)
        {
            _logger.Info("===============================================================");
            _logger.Info($"  ВНИМАНИЕ: не удалось прочитать {_failedFiles.Count} файл(ов) конфигурации:");
            foreach (var f in _failedFiles)
                _logger.Info($"    - {f}");
            _logger.Info("  Для них взяты значения по умолчанию. Смотрите строки [ERROR] выше:");
            _logger.Info("  там указаны файл, строка и позиция ошибки.");
            _logger.Info($"  Каталог конфигов: {directory}");
            _logger.Info("  Починив файлы, примените их командой css_reload_advert.");
            _logger.Info("===============================================================");
        }

        // Вывод предупреждений и информации
        if (warnings.Count > 0)
        {
            _logger.Info("⚠ Configuration Warnings:");
            foreach (var warning in warnings)
                _logger.Info($"  ⚠ {warning}");
        }

        if (info.Count > 0)
        {
            foreach (var i in info)
                _logger.Info($"  ℹ {i}");
        }

        _logger.Info($"✓ Configuration loaded from: {directory}");
    }
}
