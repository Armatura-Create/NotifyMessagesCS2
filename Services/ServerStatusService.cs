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

    /// Единоразовый начальный опрос (без анонса) - выполняется последовательно через таймеры
    public void InitialQuery()
    {
        if (_config.Servers == null)
        {
            _logger.Debug("[ServerStatus] InitialQuery skipped: Servers config is null");
            return;
        }
        
        if (!_config.Servers.Enabled)
        {
            _logger.Debug("[ServerStatus] InitialQuery skipped: Servers.Enabled = false");
            return;
        }
        
        if (_config.Servers.List.Count == 0)
        {
            _logger.Debug("[ServerStatus] InitialQuery skipped: Servers.List is empty");
            return;
        }
        
        _logger.Debug($"[ServerStatus] InitialQuery started for {_config.Servers.List.Count} server(s)");
        
        // Запускаем через таймеры последовательно - по 0.1 сек на каждый сервер
        // Это избегает проблем с Task.Run и cross-thread доступом
        var serverList = _config.Servers.List.ToList();
        var timeoutMs = _config.Servers.QueryTimeoutMs is > 0 and <= 5000 
            ? _config.Servers.QueryTimeoutMs.Value 
            : 500; // Уменьшаем таймаут чтобы не блокировать надолго
        
        float delay = 1.0f; // Начальная задержка
        foreach (var server in serverList)
        {
            var s = server; // Захватываем для замыкания
            _addTimerSimple(delay, () =>
            {
                _logger.Debug($"[ServerStatus] Querying {s.Ip}:{s.Port}...");
                try
                {
                    // Блокирующий вызов - выполняется в главном потоке
                    QueryAndStoreAsync(s, timeoutMs).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.Error($"[ServerStatus] Query failed for {s.Ip}:{s.Port} - {ex.GetType().Name}: {ex.Message}");
                }
            });
            delay += 0.1f; // Запросы с интервалом 100ms
        }
        
        // После всех запросов показываем статистику
        _addTimerSimple(delay + 0.5f, () =>
        {
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
            _logger.Debug($"[ServerStatus] Initial query completed: {onlineCount} online, {offlineCount} offline (total: {serverList.Count})");
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
            // Копируем данные из конфига в главном потоке (внутри таймера)
            var serverList = _config.Servers.List.ToList();
            var timeoutMs = _config.Servers.QueryTimeoutMs is > 0 and <= 5000
                ? _config.Servers.QueryTimeoutMs.Value
                : 500; // Уменьшенный таймаут для периодических запросов
            var ttlSeconds = _config.Servers.CacheTtlSeconds is >= 0 and <= 60
                ? _config.Servers.CacheTtlSeconds.Value
                : 5;
            
            // Последовательные запросы в главном потоке через вложенные таймеры
            var now = DateTime.UtcNow;
            var serversToQuery = new List<ServerData>();
            
            foreach (var s in serverList)
            {
                var key = (s.Ip, s.Port);
                bool needQuery;
                lock (_cacheLock)
                {
                    needQuery = !_serverCache.TryGetValue(key, out var entry) ||
                                (ttlSeconds == 0) ||
                                (now - entry.UpdatedAtUtc).TotalSeconds >= ttlSeconds;
                }
                if (needQuery)
                    serversToQuery.Add(s);
            }

            if (serversToQuery.Count > 0)
            {
                float delay = 0.05f;
                foreach (var s in serversToQuery)
                {
                    var server = s;
                    _addTimerSimple(delay, () =>
                    {
                        try
                        {
                            QueryAndStoreAsync(server, timeoutMs).GetAwaiter().GetResult();
                        }
                        catch (Exception ex)
                        {
                            _logger.Debug($"[ServerStatus] Periodic query failed for {server.Ip}:{server.Port} - {ex.Message}");
                        }
                    });
                    delay += 0.05f;
                }
                
                _addTimerSimple(delay + 0.1f, () =>
                {
                    _logger.Debug($"[ServerStatus] Periodic update completed for {serversToQuery.Count} server(s)");
                });
            }
        }, TimerFlags.REPEAT));
    }

    /// Останавливает периодический опрос
    public void Stop()
    {
        foreach (var t in _timers) t.Kill();
        _timers.Clear();
    }

    /// Принудительно запустить обновление кеша (например, после показа списка серверов)
    public void TriggerBackgroundUpdate()
    {
        if (_config.Servers == null || !_config.Servers.Enabled || _config.Servers.List.Count == 0)
            return;

        // Копируем данные из конфига в главном потоке
        var serverList = _config.Servers.List.ToList();
        var timeoutMs = _config.Servers.QueryTimeoutMs is > 0 and <= 5000
            ? _config.Servers.QueryTimeoutMs.Value
            : 500;

        // Последовательные запросы через таймеры
        float delay = 0.05f;
        foreach (var s in serverList)
        {
            var server = s;
            _addTimerSimple(delay, () =>
            {
                try
                {
                    QueryAndStoreAsync(server, timeoutMs).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.Debug($"[ServerStatus] Background query failed for {server.Ip}:{server.Port} - {ex.Message}");
                }
            });
            delay += 0.05f;
        }
        
        // После всех запросов показываем статистику
        _addTimerSimple(delay + 0.1f, () =>
        {
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
            _logger.Debug($"[ServerStatus] Background update completed: {onlineCount} online, {offlineCount} offline (total: {serverList.Count})");
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
            
            _logger.Debug($"[ServerStatus] {serverInfo.Ip}:{serverInfo.Port} - {(info != null ? "ONLINE" : "OFFLINE")}");
        }
        catch
        {
            // Не логируем детали - просто помечаем сервер как оффлайн
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
        if (_config.Servers == null || !_config.Servers.Enabled || _config.Servers.List.Count == 0)
        {
            _logger.Debug($"[ServerStatus] AnnounceToPlayer called but servers disabled or empty");
            return;
        }

        _logger.Debug($"[ServerStatus] Showing server list to {controller.PlayerName}");

        if (!string.IsNullOrEmpty(_config.TitleAnnounceServers))
            print(HudDestination.Chat, _config.TitleAnnounceServers!, controller, true);

        var snapshot = GetSnapshot();
        _logger.Debug($"[ServerStatus] Cache snapshot contains {snapshot.Count} server(s)");
        
        if (snapshot.Count == 0)
        {
            _logger.Debug($"[ServerStatus] Cache is empty, servers may not have been queried yet");
            return;
        }

        foreach (var entry in snapshot.OrderBy(v => v.Chat))
        {
            var msg = processor.ProcessMessage(entry.Chat, controller.SteamID);
            if (!string.IsNullOrEmpty(msg))
            {
                _logger.Debug($"[ServerStatus] Sending chat: {msg}");
                print(HudDestination.Chat, msg, controller, true);
            }
        }

        foreach (var entry in snapshot)
        {
            var msg = processor.ProcessMessage(entry.Console, controller.SteamID);
            if (!string.IsNullOrEmpty(msg))
                print(HudDestination.Console, msg, controller, true);
        }
        
        _logger.Debug($"[ServerStatus] Finished showing server list");
    }
}
