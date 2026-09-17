using System;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;

namespace NotifyMessages;

/// Плагин под SwiftlyS2.
///
/// SwiftlyS2 — не надстройка над Metamod, а альтернативный лоадер: он подключается
/// строкой `Game csgo/addons/swiftlys2` в gameinfo.gi и Metamod не требует.
/// Поэтому это отдельная реализация, а не порт-обёртка над версией для CSSharp:
/// код продублирован сознательно, чтобы цели не тянули друг друга. Раскладка файлов
/// повторяет cssharp/, чтобы цели можно было сравнивать глазами.
///
/// MinimumAPIVersion держим равным версии пакета, против которого собираемся
/// (ApiVersionTests это проверяет): собираться против МИНИМАЛЬНОЙ поддерживаемой
/// версии — единственный способ дать компилятору доказать, что API из более свежих
/// сборок не используется.
[PluginMetadata(
    Id = "NotifyMessages",
    Name = "NotifyMessages",
    Version = GeneratedVersion.Value,
    Author = "Armatura",
    Description = "Уведомления, реклама, приветствия и мониторинг серверов для CS2",
    Website = "https://github.com/Armatura-Create/NotifyMessagesCS2",
    MinimumAPIVersion = SwiftlyApiVersion)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001",
    Justification = "Жизненный цикл задаёт SwiftlyS2: ресурсы освобождает Unload, " +
                    "который фреймворк вызывает сам. IDisposable он не вызывает, " +
                    "и Dispose, которого никто не зовёт, только маскировал бы " +
                    "настоящую точку освобождения.")]
public sealed partial class NotifyMessages : BasePlugin
{
    /// Обязана совпадать с версией пакета SwiftlyS2.CS2 в Swiftly.props.
    internal const string SwiftlyApiVersion = "1.4.9";

    // Версия для логов берётся из метаданных сборки — тот же источник, что и
    // GeneratedVersion, но с префиксом "v" и без хвоста SourceLink.
    private static readonly string PluginVersion = PluginText.ResolveModuleVersion(typeof(PluginText).Assembly);

    // Задержка перед сообщением о входе — игрок должен успеть догрузиться
    private const float JoinAnnounceDelaySeconds = 3.0f;

    private SessionService _sessionService = null!;
    private DisplayService _displayService = null!;

    public Config Config { get; private set; } = null!;

    // Сервисы
    private ILogger _logger = null!;
    private ConfigService _configService = null!;
    private GeoIpService _geoIpService = null!;
    private MessageProcessor _messageProcessor = null!;
    private ServerStatusService _serverStatusService = null!;
    private LanguageIndex _languageIndex = null!;
    private AdvertisementService _advertisementService = null!;
    private EngineServerInfo _serverInfo = null!;

    public NotifyMessages(ISwiftlyCore core) : base(core)
    {
    }

    public override void Load(bool hotReload)
    {
        // Логгер плагина пишет через Core.Logger: свой Console.WriteLine потерял бы
        // и метку плагина, и маршрутизацию в файловые синки SwiftlyS2.
        _logger = new PluginLogger(Core.Logger, () => Config?.Debug == true);
        _configService = new ConfigService(_logger);
        Config = LoadConfigSafely();

        _logger.Info($"NotifyMessages {PluginVersion} (SwiftlyS2)");

        // Базы GeoLite2 лежат рядом с DLL плагина
        _geoIpService = new GeoIpService(Core.PluginPath, _logger);
        _sessionService = new SessionService();
        _languageIndex = LanguageIndex.Build(Config);
        // Конструктор EngineServerInfo нативов не трогает — они читаются лениво из событий
        _serverInfo = new EngineServerInfo(Core);
        _messageProcessor = new MessageProcessor(Config, ResolveLanguage, _serverInfo);
        _displayService = new DisplayService(Config, _messageProcessor, _logger, () => Core.PlayerManager.GetAllPlayers());
        _serverStatusService = CreateServerStatusService();
        _advertisementService = CreateAdvertisementService();

        RegisterEvents();
        RegisterCommands();

        // Дальше идут необязательные подсистемы. Кривой Servers.json или Ads.json не должен
        // валить весь плагин — логируем и продолжаем без этой части.
        SafeRun("первичный опрос серверов", () => _serverStatusService.InitialQuery());
        SafeRun("реклама", () => _advertisementService.Start());
        SafeRun("периодический опрос серверов", () => _serverStatusService.Start());

        if (!hotReload) return;

        SafeRun("восстановление гео-кеша после hot reload", CacheGeoForEveryone);
    }

    public override void Unload()
    {
        try { _advertisementService?.Stop(); } catch (Exception) { /* выгружаемся в любом случае */ }
        try { _serverStatusService?.Stop(); } catch (Exception) { /* выгружаемся в любом случае */ }

        UnregisterEvents();
        UnregisterCommands();

        _sessionService?.Clear();
        _geoIpService?.ClearPlayers();
        _serversCommandCooldown.Clear();

        try { _geoIpService?.Dispose(); } catch (Exception) { /* выгружаемся в любом случае */ }
    }

    /// Язык игрока: сначала язык интерфейса игры (cl_language), потом география, потом дефолт.
    /// Гео остаётся источником {COUNTRY}/{CITY} — там оно и уместно.
    ///
    /// Если на входе язык снять не удалось (userinfo ещё пуст), дочитываем его в момент
    /// первого сообщения: к этому времени клиент уже всё прислал. Один поиск игрока
    /// по SteamID на первое сообщение, дальше — кеш сессии.
    private string? ResolveLanguage(ulong steamId)
    {
        var language = _sessionService.GetLanguage(steamId);

        if (language == null && steamId != 0)
        {
            var player = FindConnectedPlayer(steamId);
            if (player != null)
            {
                language = ReadClientLanguage(player);
                _sessionService.SetLanguage(steamId, language);
            }
        }

        return _languageIndex.Resolve(language, _geoIpService.GetIsoForSteamId(steamId), Config.DefaultLang);
    }

    /// Загрузка конфигурации, которая не роняет плагин.
    /// Пустой Config безопасен: все подсистемы проверяют свои секции на null и просто молчат.
    private Config LoadConfigSafely()
    {
        try
        {
            // Core.Configuration.BasePath — это уже configs/plugins/NotifyMessages,
            // каталог целиком, а не корень сервера.
            return _configService.LoadOrCreate(Core.Configuration.BasePath);
        }
        catch (Exception ex)
        {
            _logger.Error(
                "[Load] Конфигурацию загрузить не удалось — плагин стартует с пустыми настройками " +
                "и ничего показывать не будет. Проверьте addons/swiftlys2/configs/plugins/NotifyMessages " +
                "и выполните sw_reload_advert", ex);
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

    /// Периодический таймер фреймворка в виде «как остановить»: сервисы не знают про Scheduler.
    /// DelayAndRepeatBySeconds, а не RepeatBySeconds: последний выполняет задачу сразу же,
    /// а первый показ рекламы нужен не раньше, чем через интервал — как в cssharp/.
    private Action RepeatEvery(float intervalSeconds, Action action)
    {
        var timer = Core.Scheduler.DelayAndRepeatBySeconds(intervalSeconds, intervalSeconds, action);
        return () => timer.Cancel();
    }

    /// Одноразовый таймер в том же виде.
    private Action DelayOnce(float delaySeconds, Action action)
    {
        var timer = Core.Scheduler.DelayBySeconds(delaySeconds, action);
        return () => timer.Cancel();
    }

    private ServerStatusService CreateServerStatusService() => new(
        Config,
        _logger,
        RepeatEvery,
        // Scheduler.NextTick — штатный способ вернуться в главный поток из фона
        action => Core.Scheduler.NextTick(action));

    private AdvertisementService CreateAdvertisementService() => new(
        Config,
        _logger,
        RepeatEvery,
        (channel, message) => _displayService.Print(channel, message));

    /// Гео для всех, кто уже на сервере. Нужно после hot reload и sw_reload_advert.
    private void CacheGeoForEveryone()
    {
        _geoIpService.ClearPlayers();

        foreach (var player in Core.PlayerManager.GetAllPlayers())
        {
            if (player.IsFakeClient || !player.IsValid) continue;

            try
            {
                CachePlayerGeo(player, player.SteamID);
                // Иначе первая смена карты после hot reload анонсировала бы всех как новых
                _sessionService.AddFullyConnected(player.SteamID);
            }
            catch (Exception ex)
            {
                // Один невалидный игрок не должен ломать обход остальных
                _logger.Debug($"[GeoIP] Пропущен игрок при обновлении гео-кеша: {ex.Message}");
            }
        }
    }
}
