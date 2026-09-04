using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Collections.Generic;

namespace NotifyMessages;

/// Сервис работы с конфигурацией плагина: загрузка/сохранение и создание дефолтных файлов
public sealed partial class ConfigService
{
    // JsonSerializerOptions дорогие в создании и потокобезопасны — держим по одному экземпляру
    // JsonStringEnumConverter: "MessageType": "CenterHtml" вместо числа 0..4.
    // Числа продолжают читаться — старые конфиги не ломаются.
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
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

        // Схемы и README перезаписываются всегда: иначе после обновления плагина
        // они продолжают описывать старую версию и врут админу.
        // Справочные файлы — не повод сорвать загрузку конфига: каталог может быть
        // read-only, а файл занят редактором админа.
        try
        {
            WriteSchemas(directory);
            CreateConfigReadme(directory);
        }
        catch (Exception ex)
        {
            _logger.Error($"[Config] Не удалось обновить README.txt/*.schema.json в {directory}. " +
                          "Сама конфигурация читается как обычно", ex);
        }

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
            LanguageAliases = settings?.LanguageAliases,

            // Из Messages.json. Словари языков пересобираются регистронезависимыми:
            // движок отдаёт "ru", а в конфиге исторически "RU" — без этого не совпадёт.
            LanguageMessages = ToCaseInsensitive(messages?.LanguageMessages),
            JoinMessages = ToCaseInsensitiveKeys(messages?.JoinMessages),
            LeaveMessages = ToCaseInsensitiveKeys(messages?.LeaveMessages),

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

    /// Ключ -> язык -> текст: регистронезависимым делаем внутренний словарь (языки).
    /// Внешний тоже: {Prefix} и {prefix} для админа — одно и то же.
    private static Dictionary<string, Dictionary<string, string>>? ToCaseInsensitive(
        Dictionary<string, Dictionary<string, string>>? source)
    {
        if (source == null) return null;

        var result = new Dictionary<string, Dictionary<string, string>>(source.Count,
            StringComparer.OrdinalIgnoreCase);

        foreach (var (key, translations) in source)
        {
            result[key] = translations == null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(translations, StringComparer.OrdinalIgnoreCase);
        }

        return result;
    }

    private static Dictionary<string, List<string>>? ToCaseInsensitiveKeys(
        Dictionary<string, List<string>>? source)
        => source == null ? null : new Dictionary<string, List<string>>(source, StringComparer.OrdinalIgnoreCase);

    // ---- Создание дефолтных конфигов ------------------------------------------

    private Config CreateDefaultConfigs(string directory)
    {
        var settings = CreateDefaultSettings();
        var messages = CreateDefaultMessages();
        var ads = CreateDefaultAds();
        var servers = CreateDefaultServers();

        // Сохраняем каждый файл
        SaveConfig(Path.Combine(directory, "Settings.json"), settings, "Settings.schema.json");
        SaveConfig(Path.Combine(directory, "Messages.json"), messages, "Messages.schema.json");
        SaveConfig(Path.Combine(directory, "Ads.json"), ads, "Ads.schema.json");
        SaveConfig(Path.Combine(directory, "Servers.json"), servers, "Servers.schema.json");

        _logger.Info("✓ Settings.json created");
        _logger.Info("✓ Messages.json created");
        _logger.Info("✓ Ads.json created");
        _logger.Info("✓ Servers.json created");
        _logger.Info("✓ README.txt и *.schema.json созданы");
        _logger.Info("═══════════════════════════════════════════════════════════════");
        
        return MergeParts(settings, messages, ads, servers);
    }

    /// Пишет конфиг, добавляя ссылку на JSON Schema первым свойством.
    /// Благодаря ей редактор (VS Code и любой другой с поддержкой schema) даёт автодополнение
    /// полей и подсветку опечаток — это заменяет половину документации.
    /// Само свойство "$schema" System.Text.Json при чтении молча игнорирует.
    private static void SaveConfig<T>(string path, T config, string schemaFile)
    {
        var json = JsonSerializer.Serialize(config, WriteOptions);
        json = json.Insert(1, $"\n  \"$schema\": \"./{schemaFile}\",");
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

        ReportTemplateIssues(config);

        _logger.Info($"✓ Configuration loaded from: {directory}");
    }

    /// Претензии к шаблонам: неизвестные теги доезжают до игрока как текст в скобках,
    /// поэтому это Error, а не Info. Раньше такое находил только игрок.
    private void ReportTemplateIssues(Config config)
    {
        var issues = CollectIssues(config);
        if (issues.Count == 0) return;

        var errors = 0;
        foreach (var issue in issues)
        {
            if (issue.Severity == TemplateSeverity.Error)
            {
                errors++;
                _logger.Error($"[Config] {issue}");
            }
            else
            {
                _logger.Info($"[Config] {issue}");
            }
        }

        if (errors > 0)
        {
            _logger.Info($"  ⚠ Шаблонов с неизвестными тегами: {errors}. " +
                         "Проверьте командой css_nm_check, посмотрите результат командой css_nm_preview.");
        }
    }
}
