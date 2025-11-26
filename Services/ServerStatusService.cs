using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace NotifyMessages;

/// Сервис статусов серверов: опрос A2S, кеш и таймеры
public sealed class ServerStatusService
{
    private readonly Config _config;
    private readonly ILogger _logger;
    private readonly Func<float, Action, Timer> _addTimerSimple;
    private readonly Func<float, Action, TimerFlags, Timer> _addTimer;
    private readonly Action<Action> _postToMainThread;

    private readonly object _cacheLock = new();
    private readonly Dictionary<(string ip, int port), ServerCacheEntry> _serverCache = new();
    private readonly List<Timer> _timers = new();

    public ServerStatusService(
        Config config,
        ILogger logger,
        Func<float, Action, Timer> addTimerSimple,
        Func<float, Action, TimerFlags, Timer> addTimer,
        Action<Action> postToMainThread)
    {
        _config = config;
        _logger = logger;
        _addTimerSimple = addTimerSimple;
        _addTimer = addTimer;
        _postToMainThread = postToMainThread;
    }

    /// Единоразовый начальный опрос (без анонса) - выполняется асинхронно в фоне
    public void InitialQuery()
    {
        if (_config.Servers == null || !_config.Servers.Enabled || _config.Servers.List.Count == 0) return;
        
        // Запускаем в фоне, чтобы не блокировать загрузку плагина
        _ = Task.Run(async () =>
        {
            try
            {
                var timeoutMs = _config.Servers.QueryTimeoutMs is > 0 and <= 5000 ? _config.Servers.QueryTimeoutMs.Value : 1000;

                var tasks = _config.Servers.List
                    .Select(s => QueryAndStoreAsync(s, timeoutMs))
                    .ToArray();

                await Task.WhenAll(tasks).ConfigureAwait(false);
                
                // Подсчитываем онлайн/оффлайн серверов
                int onlineCount = 0;
                int offlineCount = 0;
                lock (_cacheLock)
                {
                    foreach (var entry in _serverCache.Values)
                    {
                        if (entry.Online) onlineCount++;
                        else offlineCount++;
                    }
                }
                
                var count = _config.Servers.List.Count;
                _postToMainThread(() => _logger.Debug($"[ServerStatus] Initial query completed: {onlineCount} online, {offlineCount} offline (total: {count})"));
            }
            catch (Exception ex)
            {
                _postToMainThread(() => _logger.Error("[ServerStatus] Initial query failed", ex));
            }
        });
    }

    /// Запускает периодический опрос и обновляет кеш
    public void Start()
    {
        if (_config.Servers == null || !_config.Servers.Enabled || _config.Servers.List.Count == 0)
            return;

        var interval = Math.Max(5f, _config.Servers.Interval);
        _timers.Add(_addTimer(interval, () =>
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var timeoutMs = _config.Servers.QueryTimeoutMs is > 0 and <= 5000
                        ? _config.Servers.QueryTimeoutMs.Value
                        : 1000;

                    var ttlSeconds = _config.Servers.CacheTtlSeconds is >= 0 and <= 60
                        ? _config.Servers.CacheTtlSeconds.Value
                        : 5;

                    var now = DateTime.UtcNow;
                    var tasks = new List<Task>();
                    foreach (var s in _config.Servers.List)
                    {
                        var key = (s.Ip, s.Port);
                        bool needQuery;
                        lock (_cacheLock)
                        {
                            needQuery = !_serverCache.TryGetValue(key, out var entry) ||
                                        (ttlSeconds == 0) ||
                                        (now - entry.UpdatedAtUtc).TotalSeconds >= ttlSeconds;
                        }
                        if (!needQuery) continue;
                        tasks.Add(QueryAndStoreAsync(s, timeoutMs));
                    }

                    if (tasks.Count > 0)
                    {
                        await Task.WhenAll(tasks).ConfigureAwait(false);
                        var taskCount = tasks.Count;
                        _postToMainThread(() => _logger.Debug($"[ServerStatus] Periodic update completed for {taskCount} server(s)"));
                    }
                }
                catch (Exception ex)
                {
                    _postToMainThread(() => _logger.Error("[ServerStatus] Periodic update failed", ex));
                }
            });
        }, TimerFlags.REPEAT));
    }

    /// Останавливает периодический опрос
    public void Stop()
    {
        foreach (var t in _timers) t.Kill();
        _timers.Clear();
    }

    /// Принудительно запустить обновление кеша в фоне (например, после показа списка серверов)
    public void TriggerBackgroundUpdate()
    {
        if (_config.Servers == null || !_config.Servers.Enabled || _config.Servers.List.Count == 0)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                var timeoutMs = _config.Servers.QueryTimeoutMs is > 0 and <= 5000
                    ? _config.Servers.QueryTimeoutMs.Value
                    : 1000;

                var tasks = _config.Servers.List
                    .Select(s => QueryAndStoreAsync(s, timeoutMs))
                    .ToArray();

                await Task.WhenAll(tasks).ConfigureAwait(false);
                
                // Подсчитываем онлайн/оффлайн серверов
                int onlineCount = 0;
                int offlineCount = 0;
                lock (_cacheLock)
                {
                    foreach (var entry in _serverCache.Values)
                    {
                        if (entry.Online) onlineCount++;
                        else offlineCount++;
                    }
                }
                
                var count = _config.Servers.List.Count;
                _postToMainThread(() => _logger.Debug($"[ServerStatus] Background update completed: {onlineCount} online, {offlineCount} offline (total: {count})"));
            }
            catch (Exception ex)
            {
                _postToMainThread(() => _logger.Error("[ServerStatus] Background update failed", ex));
            }
        });
    }

    /// Снимок кеша (копия значений) — безопасно и удобно сортировать снаружи
    public IReadOnlyCollection<ServerCacheEntry> GetSnapshot()
    {
        lock (_cacheLock)
        {
            return _serverCache.Values.ToList();
        }
    }

    private async Task QueryAndStoreAsync(ServerData serverInfo, int timeoutMs)
    {
        try
        {
            using var cts = new CancellationTokenSource(timeoutMs + 250);
            var info = await AdvancedA2S.GetServerInfoAsync(serverInfo.Ip, (ushort)serverInfo.Port, timeoutMs, cts.Token)
                .ConfigureAwait(false);

            var (chat, console) = BuildServerLines(serverInfo, info);

            lock (_cacheLock)
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
            // Логируем через главный поток чтобы избежать проблем с CS2 API
            var errorMsg = $"Query error {serverInfo.Ip}:{serverInfo.Port}";
            _postToMainThread(() => _logger.Error(errorMsg, ex));
            
            lock (_cacheLock)
            {
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

    /// Объявить список серверов игроку, используя текущий снимок кеша
    public void AnnounceToPlayer(CCSPlayerController controller, MessageProcessor processor,
        Action<HudDestination?, string, CCSPlayerController?, bool> print)
    {
        if (_config.Servers == null || !_config.Servers.Enabled || _config.Servers.List.Count == 0) return;

        if (!string.IsNullOrEmpty(_config.TitleAnnounceServers))
            print(HudDestination.Chat, _config.TitleAnnounceServers!, controller, true);

        var snapshot = GetSnapshot();
        foreach (var entry in snapshot.OrderBy(v => v.Chat))
        {
            var msg = processor.ProcessMessage(entry.Chat, controller.SteamID);
            if (!string.IsNullOrEmpty(msg))
                print(HudDestination.Chat, msg, controller, true);
        }

        foreach (var entry in snapshot)
        {
            var msg = processor.ProcessMessage(entry.Console, controller.SteamID);
            if (!string.IsNullOrEmpty(msg))
                print(HudDestination.Console, msg, controller, true);
        }
    }
}
