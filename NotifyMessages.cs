using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;

namespace NotifyMessages;

[MinimumApiVersion(339)]
public partial class NotifyMessages : BasePlugin
{
    public override string ModuleAuthor => "Armatura";
    public override string ModuleName => "NotifyMessages";
    public override string ModuleVersion => "v2.0.0";


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
        LogService.Current = _logger;
        _configService = new ConfigService(_logger);
        Config = _configService.LoadOrCreate(Application.RootDirectory);
        _geoIpService = new GeoIpService(ModuleDirectory, _logger);
        _messageProcessor = new MessageProcessor(Config,
            steamId => _geoIpService.GetIsoForSteamId(steamId) ?? Config.DefaultLang);
        _sessionService = new SessionService();
        _displayService = new DisplayService(Config, _messageProcessor, _logger);
        _serverStatusService = new ServerStatusService(
            Config,
            _logger,
            (interval, action) => AddTimer(interval, action),
            (interval, action, flags) => AddTimer(interval, action, flags),
            action => AddTimer(0.0f, action));
        _advertisementService = new AdvertisementService(
            Config,
            _messageProcessor,
            _logger,
            (interval, action, flags) => AddTimer(interval, action, flags),
            _displayService.Print);

        RegisterEvents();

        _serverStatusService.InitialQuery(); // первичное заполнение кеша
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
        _geoIpService.ClearPlayers();
        _displayService?.ClearUsers();

        try { _geoIpService?.Dispose(); } catch { /* ignore */ }
    }
}