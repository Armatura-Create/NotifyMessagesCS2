using System;
using System.Reflection;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;

namespace NotifyMessages;

// Минимум = 1.0.369: первая версия CounterStrikeSharp на .NET 10.
// Не поднимать без реальной нужды — это отсечёт сервера, на которых плагин работает.
[MinimumApiVersion(369)]
public partial class NotifyMessages : BasePlugin
{
    public override string ModuleAuthor => "Armatura";
    public override string ModuleName => "NotifyMessages";
    // Версия НЕ хардкодится: берётся из метаданных сборки, которые проставляет MSBuild
    // из <Version>, а в релизе — из тега (см. .github/workflows/release.yml).
    // Раньше её надо было помнить поднять руками, и релиз v2.1.1 уехал с "v2.1.0" внутри.
    public override string ModuleVersion => PluginVersion;

    private static readonly string PluginVersion = ResolveModuleVersion(typeof(NotifyMessages).Assembly);

    /// Версия сборки в виде "vX.Y.Z" (с суффиксом pre-release, если он есть).
    /// InformationalVersion может нести хвост "+Sha.abc123" от SourceLink — его отрезаем.
    internal static string ResolveModuleVersion(Assembly assembly) => FormatModuleVersion(
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
        assembly.GetName().Version);

    /// Чистая часть резолва — вынесена ради тестов.
    internal static string FormatModuleVersion(string? informationalVersion, Version? assemblyVersion)
    {
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var plus = informationalVersion.IndexOf('+');
            var trimmed = plus >= 0 ? informationalVersion[..plus] : informationalVersion;
            if (!string.IsNullOrWhiteSpace(trimmed))
                return "v" + trimmed.Trim();
        }

        return assemblyVersion == null ? "v0.0.0" : "v" + assemblyVersion.ToString(3);
    }

    // Задержка перед сообщением о входе — игрок должен успеть догрузиться
    private const float JoinAnnounceDelaySeconds = 3.0f;

    private SessionService _sessionService = null!;
    private DisplayService _displayService = null!;

    public Config Config { get; set; } = null!;

    // Сервисы
    private ILogger _logger = null!;
    private ConfigService _configService = null!;
    private GeoIpService _geoIpService = null!;
    private MessageProcessor _messageProcessor = null!;
    private ServerStatusService _serverStatusService = null!;
    private LanguageIndex _languageIndex = null!;
    private AdvertisementService _advertisementService = null!;

    public override void Load(bool hotReload)
    {
        _logger = new PluginLogger(() => Config?.Debug == true);
        _configService = new ConfigService(_logger);
        Config = LoadConfigSafely();
        _geoIpService = new GeoIpService(ModuleDirectory, _logger);
        _sessionService = new SessionService();
        _languageIndex = LanguageIndex.Build(Config);
        _messageProcessor = new MessageProcessor(Config, ResolveLanguage);
        _displayService = new DisplayService(Config, _messageProcessor, _logger);
        _serverStatusService = CreateServerStatusService();
        _advertisementService = CreateAdvertisementService();

        RegisterEvents();

        // Дальше идут необязательные подсистемы. Кривой Servers.json или Ads.json не должен
        // валить весь плагин — логируем и продолжаем без этой части.
        SafeRun("первичный опрос серверов", () => _serverStatusService.InitialQuery());
        SafeRun("реклама", () => _advertisementService.Start());
        SafeRun("периодический опрос серверов", () => _serverStatusService.Start());

        if (!hotReload) return;

        SafeRun("восстановление гео-кеша после hot reload", () =>
        {
            _geoIpService.ClearPlayers();

            foreach (var player in Utilities.GetPlayers())
            {
                if (player.IsBot || !player.IsValid) continue;
                CachePlayerGeo(player, player.SteamID);
            }
        });
    }

    /// Язык игрока: сначала выбор самого игрока (cl_language), потом география, потом дефолт.
    /// Гео остаётся источником {COUNTRY}/{CITY} — там оно и уместно.
    private string? ResolveLanguage(ulong steamId) => _languageIndex.Resolve(
        _sessionService.GetLanguage(steamId),
        _geoIpService.GetIsoForSteamId(steamId),
        Config.DefaultLang);

    /// Загрузка конфигурации, которая не роняет плагин.
    /// Пустой Config безопасен: все подсистемы проверяют свои секции на null и просто молчат.
    private Config LoadConfigSafely()
    {
        try
        {
            return _configService.LoadOrCreate(Application.RootDirectory);
        }
        catch (Exception ex)
        {
            _logger.Error(
                "[Load] Конфигурацию загрузить не удалось — плагин стартует с пустыми настройками " +
                "и ничего показывать не будет. Проверьте configs/plugins/NotifyMessages " +
                "и выполните css_reload_advert", ex);
            return new Config();
        }
    }

    /// Запускает необязательную подсистему, не давая её падению сорвать загрузку плагина
    private void SafeRun(string what, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _logger.Error($"[Load] Подсистема отключена из-за ошибки: {what}", ex);
        }
    }

    private ServerStatusService CreateServerStatusService() => new(
        Config,
        _logger,
        (interval, action, flags) => AddTimer(interval, action, flags));

    private AdvertisementService CreateAdvertisementService() => new(
        Config,
        _logger,
        (interval, action, flags) => AddTimer(interval, action, flags),
        // Лямбда, а не метод-группа: у Print есть необязательный параметр values
        (channel, message, target) => _displayService.Print(channel, message, target));

    private void OnTick()
    {
        _displayService.OnTick();
    }

    public override void Unload(bool hotReload)
    {
        // Stop timers/services
        try { _advertisementService?.Stop(); } catch { /* ignore */ }
        try { _serverStatusService?.Stop(); } catch { /* ignore */ }
        _sessionService?.Clear();

        // Clear state
        _geoIpService?.ClearPlayers();
        _displayService?.ClearUsers();
        _serversCommandCooldown.Clear();

        try { _geoIpService?.Dispose(); } catch { /* ignore */ }
    }
}
