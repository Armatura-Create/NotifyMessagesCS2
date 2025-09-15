using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using MaxMind.GeoIP2;
using Server = CounterStrikeSharp.API.Server;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace NotifyMessages;


[MinimumApiVersion(339)]
public partial class NotifyMessages : BasePlugin
{
    public override string ModuleAuthor => "Armatura";
    public override string ModuleName => "NotifyMessages";
    public override string ModuleVersion => "v2.0.0";

    private readonly List<Timer> _timers = [];
    private readonly List<Timer> _serverTimers = [];

    private readonly Dictionary<ulong, Timer> _connectionTimers = new();
    private readonly HashSet<ulong> _fullyConnectedPlayers = new();

    // Для определения страны/города
    private readonly Dictionary<ulong, string> _playerIsoCode = new();
    private readonly Dictionary<ulong, string> _playerCity = new();

    // ── кеш статусов серверов ────────────────────────────────────────────────────
    private readonly object _serverCacheLock = new();

    private readonly Dictionary<(string ip, int port), ServerCacheEntry> _serverCache = new();

    // Пользовательские данные по слотам
    private readonly User?[] _users = new User?[66];

    public Config Config { get; set; } = null!;

        // Сервисы
        private ILogger _logger = null!;
        private ConfigService _configService = null!;
        private GeoIpService _geoIpService = null!;
        private MessageProcessor _messageProcessor = null!;
        private ServerStatusService _serverStatusService = null!;

    public override void Load(bool hotReload)
    {
        _logger = new PluginLogger(() => Config?.Debug == true);
        LogService.Current = _logger;
        _configService = new ConfigService(_logger);
        Config = _configService.LoadOrCreate(Application.RootDirectory);
        _geoIpService = new GeoIpService(ModuleDirectory, _logger);
        _messageProcessor = new MessageProcessor(Config, steamId => _playerIsoCode.TryGetValue(steamId, out var code) ? code : Config.DefaultLang);
        _serverStatusService = new ServerStatusService(
            Config,
            _logger,
            (interval, action) => AddTimer(interval, action),
            (interval, action, flags) => AddTimer(interval, action, flags),
            action => AddTimer(0.0f, action));

        RegisterEvents();

        InitialServerQuery(); // первичное заполнение кеша
        StartTimers(); // реклама/сообщения
        StartServerTimers(); // периодический опрос серверов

        if (!hotReload) return;

        _playerIsoCode.Clear();
        _playerCity.Clear();

        foreach (var player in Utilities.GetPlayers())
        {
            if (player.IsBot || !player.IsValid || player.AuthorizedSteamID == null) continue;
            OnClientAuthorized(player.Slot, player.AuthorizedSteamID);
        }
    }


    private string GetRandomLocalizedMessage(Dictionary<string, List<string>>? messages, ulong steamId,
        string playerName, string country, string city)
    {
        if (messages == null || messages.Count == 0) return string.Empty;

        var lang = Config.DefaultLang ?? "US";
        if (_playerIsoCode.TryGetValue(steamId, out var playerLang) && messages.ContainsKey(playerLang))
            lang = playerLang;

        if (!messages.TryGetValue(lang, out var messageList) || messageList.Count == 0) return string.Empty;

        var message = messageList[Random.Shared.Next(messageList.Count)];

        return message
            .Replace("{PLAYERNAME}", playerName)
            .Replace("{COUNTRY}", country)
            .Replace("{CITY}", city);
    }

    // --- Основные таймеры ---

    private void StartTimers()
    {
        if (Config.Ads == null) return;
        foreach (var ad in Config.Ads)
        {
            _timers.Add(AddTimer(ad.Interval, () => ShowAd(ad), TimerFlags.REPEAT));
        }
    }

    private void ShowAd(Advertisement ad)
    {
        var messages = ad.NextMessages;
        foreach (var (type, message) in messages)
        {
            switch (type)
            {
                case "Chat":
                    PrintWrappedLine(HudDestination.Chat, message);
                    break;
                case "Center":
                    PrintWrappedLine(HudDestination.Center, message);
                    break;
                case "Console":
                    PrintWrappedLine(HudDestination.Console, message);
                    break;
            }
        }
    }

    // ── Опрос серверов ───────────────────────────────────────────────────────────

    private void StartServerTimers()
    {
        _serverStatusService.Start();
    }

    private async Task QueryAndStoreAsync(ServerData serverInfo, int timeoutMs)
    {
        try
        {
            using var cts = new CancellationTokenSource(timeoutMs + 250);
            var info = await AdvancedA2S
                .GetServerInfoAsync(serverInfo.Ip, (ushort)serverInfo.Port, timeoutMs, cts.Token)
                .ConfigureAwait(false);

            var (chat, console) = BuildServerLines(serverInfo, info);

            lock (_serverCacheLock)
            {
                _serverCache[(serverInfo.Ip, serverInfo.Port)] = new ServerCacheEntry
                {
                    Chat = chat,
                    Console = console,
                    Online = info != null,
                    UpdatedAtUtc = DateTime.UtcNow
                };
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Query error {serverInfo.Ip}:{serverInfo.Port}", ex);
            lock (_serverCacheLock)
            {
                // записываем OFFLINE в кеш, чтобы не «молчать»
                var (chat, console) = BuildServerLines(serverInfo, null);
                _serverCache[(serverInfo.Ip, serverInfo.Port)] = new ServerCacheEntry
                {
                    Chat = chat,
                    Console = console,
                    Online = false,
                    UpdatedAtUtc = DateTime.UtcNow
                };
            }
        }
    }

    private (string chat, string console) BuildServerLines(ServerData s, A2SInfoResponse? info)
    {
        // оффлайн — показываем OFFLINE/0/x/unknown map
        string map = info?.Map.Trim() ?? "OFFLINE";
        string players = info != null ? Math.Max(info.Players - info.Bots, 0).ToString() : "0";
        string max = info?.MaxPlayers.ToString() ?? s.MaxPlayersFallback?.ToString() ?? "?";

        var msgChat = (s.MessageTemplate?.Length > 0
                ? s.MessageTemplate
                : "{SERVER_IP}:{SERVER_PORT} - {SERVER_MAP} | {SERVER_PLAYERS}/{SERVER_MAXPLAYERS}")
            .Replace("{SERVER_IP}", s.Ip)
            .Replace("{SERVER_PORT}", s.Port.ToString())
            .Replace("{SERVER_MAP}", map)
            .Replace("{SERVER_PLAYERS}", players)
            .Replace("{SERVER_MAXPLAYERS}", max);

        var msgConsole = (s.MessageTemplateConsole?.Length > 0 ? s.MessageTemplateConsole : msgChat)
            .Replace("{SERVER_IP}", s.Ip)
            .Replace("{SERVER_PORT}", s.Port.ToString())
            .Replace("{SERVER_MAP}", map)
            .Replace("{SERVER_PLAYERS}", players)
            .Replace("{SERVER_MAXPLAYERS}", max);

        return (msgChat, msgConsole);
    }


    /// <summary>Единоразовый начальный опрос всех серверов (без анонса).</summary>
    private void InitialServerQuery()
    {
        _serverStatusService.InitialQuery();
    }

    // --- Вывод списка серверов ---

    private void AnnounceServersInChat()
    {
        var players = Utilities.GetPlayers().Where(u => !u.IsBot && u.IsValid);
        if (!players.Any()) return;

        if (!string.IsNullOrEmpty(Config.TitleAnnounceServers))
            PrintWrappedLine(HudDestination.Chat, Config.TitleAnnounceServers!);

        var snapshot = _serverStatusService.GetSnapshot();
        foreach (var entry in snapshot.OrderBy(v => v.Chat))
        {
            var formatted = _messageProcessor.ProcessMessage(entry.Chat, 0);
            if (!string.IsNullOrEmpty(formatted))
                PrintWrappedLine(HudDestination.Chat, formatted);
        }

        foreach (var entry in snapshot)
        {
            var msg = _messageProcessor.ProcessMessage(entry.Console, 0);
            if (!string.IsNullOrEmpty(msg))
                PrintWrappedLine(HudDestination.Console, msg);
        }
    }

    private void AnnounceServersToPlayer(CCSPlayerController controller)
    {
        if (!string.IsNullOrEmpty(Config.TitleAnnounceServers))
            PrintWrappedLine(HudDestination.Chat, Config.TitleAnnounceServers, controller, true);

        var snapshot = _serverStatusService.GetSnapshot();
        foreach (var entry in snapshot.OrderBy(v => v.Chat))
        {
            var msg = _messageProcessor.ProcessMessage(entry.Chat, controller.SteamID);
            if (!string.IsNullOrEmpty(msg))
                PrintWrappedLine(HudDestination.Chat, msg, controller, true);
        }

        foreach (var entry in snapshot)
        {
            var msg = _messageProcessor.ProcessMessage(entry.Console, controller.SteamID);
            if (!string.IsNullOrEmpty(msg))
                PrintWrappedLine(HudDestination.Console, msg, controller, true);
        }
    }

    // --- Логика вывода рекламы (OnTick и т.д.) ---

    private void OnTick()
    {
        foreach (var player in Utilities.GetPlayers())
        {
            var user = _users[player.Slot];
            if (user == null) continue;

            if (user.HtmlPrint)
            {
                var showWhenDead = Config.ShowHtmlWhenDead ?? false;
                if (!showWhenDead && !player.PawnIsAlive)
                    continue;

                var duration = Config.HtmlCenterDuration;
                if (duration != null && TimeSpan.FromSeconds(user.PrintTime / 64.0).Seconds < duration.Value)
                {
                    player.PrintToCenterHtml(user.Message);
                    user.PrintTime++;
                }
                else
                {
                    user.HtmlPrint = false;
                }
            }
        }
    }

    // --- Вспомогательные методы ---

    private void PrintWrappedLine(HudDestination? destination, string message,
        CCSPlayerController? connectPlayer = null, bool privateMsg = false)
    {
        if (connectPlayer != null && connectPlayer is { IsValid: true, IsBot: false } && privateMsg)
        {
            var processed = _messageProcessor.ProcessMessage(message, connectPlayer.SteamID);

            switch (destination)
            {
                case HudDestination.Chat:
                    connectPlayer.PrintToChat(processed);
                    break;
                case HudDestination.Console:
                    connectPlayer.PrintToConsole(processed);
                    break;
                default:
                    if (Config.PrintToCenterHtml == true)
                        SetHtmlPrintSettings(connectPlayer, processed);
                    else
                        connectPlayer.PrintToCenter(processed);
                    break;
            }
        }
        else
        {
            foreach (var player in Utilities.GetPlayers().Where(u => !privateMsg && !u.IsBot && u.IsValid))
            {
                var processed = _messageProcessor.ProcessMessage(message, player.SteamID);

                switch (destination)
                {
                    case HudDestination.Chat:
                        player.PrintToChat(processed);
                        break;
                    case HudDestination.Console:
                        player.PrintToConsole(processed);
                        break;
                    default:
                        if (Config.PrintToCenterHtml == true)
                            SetHtmlPrintSettings(player, processed);
                        else
                            player.PrintToCenter(processed);
                        break;
                }
            }
        }

        if (!Config.Debug) return;
        {
            var processed = _messageProcessor.ProcessMessage(message, 0);
            _logger.Debug("[ADS DEBUG] " + TextFormatter.StripColorCodes(processed));
        }
    }

    private void SetHtmlPrintSettings(CCSPlayerController player, string message)
    {
        var user = _users[player.Slot];
        if (user == null)
        {
            _users[player.Slot] = new User();
            user = _users[player.Slot];
        }

        user.HtmlPrint = true;
        user.PrintTime = 0;
        user.Message = message;
    }


    private Config LoadConfig()
    {
        return _configService.LoadOrCreate(Application.RootDirectory);
    }


    public override void Unload(bool hotReload)
    {
        // Kill all timers
        foreach (var t in _timers) t.Kill();
        _timers.Clear();
        foreach (var t in _serverTimers) t.Kill();
        _serverTimers.Clear();
        foreach (var kv in _connectionTimers) kv.Value.Kill();
        _connectionTimers.Clear();

        // Clear state
        _fullyConnectedPlayers.Clear();
        _playerIsoCode.Clear();
        _playerCity.Clear();
        lock (_serverCacheLock) _serverCache.Clear();
        for (int i = 0; i < _users.Length; i++) _users[i] = null;

        // Dispose services
        try { _serverStatusService?.Stop(); } catch { /* ignore */ }
        try { _geoIpService?.Dispose(); } catch { /* ignore */ }
    }

}
