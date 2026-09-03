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
    private AdvertisementService _advertisementService = null!;

    public override void Load(bool hotReload)
    {
        _logger = new PluginLogger(() => Config?.Debug == true);
        _configService = new ConfigService(_logger);
        Config = _configService.LoadOrCreate(Application.RootDirectory);
        _geoIpService = new GeoIpService(ModuleDirectory, _logger);
        _messageProcessor = new MessageProcessor(Config,
            steamId => _geoIpService.GetIsoForSteamId(steamId) ?? Config.DefaultLang);
        _sessionService = new SessionService();
        _displayService = new DisplayService(Config, _messageProcessor, _logger);
        _serverStatusService = CreateServerStatusService();
        _advertisementService = CreateAdvertisementService();

        RegisterEvents();

        _serverStatusService.InitialQuery(); // первичное заполнение кеша (в фоне)
        _advertisementService.Start(); // реклама/сообщения
        _serverStatusService.Start(); // периодический опрос серверов

        if (!hotReload) return;

        _geoIpService.ClearPlayers();

        foreach (var player in Utilities.GetPlayers())
        {
            if (player.IsBot || !player.IsValid || player.AuthorizedSteamID == null) continue;
            OnClientAuthorized(player.Slot, player.AuthorizedSteamID);
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
        _displayService.Print);

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
