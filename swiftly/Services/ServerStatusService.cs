using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NotifyMessages;

/// Сервис статусов серверов: опрос A2S, кеш и таймеры.
///
/// Опрос выполняется в фоновом потоке и НЕ трогает нативы CS2 — только UDP-сокет,
/// строки и словарь под lock. Раньше запросы делались через GetAwaiter().GetResult()
/// прямо в главном потоке и подвешивали сервер до timeout+250 мс на каждый адрес.
///
/// Фреймворка здесь нет: таймер и возврат в главный поток приходят делегатами из плагина.
/// Поэтому класс одинаков в cssharp/ и swiftly/ и проверяется тестами без сервера.
public sealed class ServerStatusService
{
    private readonly Config _config;
    private readonly ILogger _logger;

    /// (интервал в секундах, действие) -> как остановить. Плагин оборачивает свой таймер.
    private readonly Func<float, Action, Action> _repeatEvery;

    /// Выполнить действие в главном потоке (Server.NextFrame / Scheduler.NextTick).
    private readonly Action<Action> _runOnMainThread;

    private readonly object _cacheLock = new();
    private readonly Dictionary<(string ip, int port), ServerCacheEntry> _serverCache = new();
    private readonly List<Action> _stopTimers = new();

    // Сколько серверов опрашиваем одновременно
    private const int MaxConcurrentQueries = 8;

    // 0 - свободно, 1 - опрос уже идёт. Не даём спамом команд плодить параллельные проходы.
    private int _queryInFlight;
    private volatile bool _stopped;

    public ServerStatusService(
        Config config,
        ILogger logger,
        Func<float, Action, Action> repeatEvery,
        Action<Action> runOnMainThread)
    {
        _config = config;
        _logger = logger;
        _repeatEvery = repeatEvery;
        _runOnMainThread = runOnMainThread;
    }

    /// Логирование из фонового потока маршалим в главный: логгер пишет в консоль,
    /// которую перехватывает сам фреймворк, и звать её из чужого потока — лишний риск.
    private void BgDebug(string message) => _runOnMainThread(() => _logger.Debug(message));

    private void BgError(string message, Exception? ex = null) => _runOnMainThread(() => _logger.Error(message, ex));

    private bool Enabled =>
        _config.Servers is { Enabled: true } servers && servers.List.Count > 0;

    private int TimeoutMs
    {
        get
        {
            var value = _config.Servers?.QueryTimeoutMs;
            return value is > 0 and <= 5000 ? value.Value : 500;
        }
    }

    private int CacheTtlSeconds
    {
        get
        {
            var value = _config.Servers?.CacheTtlSeconds;
            return value is >= 0 and <= 60 ? value.Value : 5;
        }
    }

    /// Единоразовый начальный опрос (без анонса)
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
        RefreshAsync(force: true, reason: "Initial query");
    }

    /// Запускает периодический опрос и обновляет кеш
    public void Start()
    {
        if (!Enabled) return;

        var interval = Math.Max(5f, _config.Servers!.Interval);
        _stopTimers.Add(_repeatEvery(interval, () => RefreshAsync(force: false, reason: "Periodic update")));
    }

    /// Останавливает периодический опрос
    public void Stop()
    {
        _stopped = true;
        foreach (var stop in _stopTimers) stop();
        _stopTimers.Clear();
    }

    /// Принудительно обновить кеш в фоне (например, после показа списка серверов)
    public void TriggerBackgroundUpdate() => RefreshAsync(force: false, reason: "Background update");

    /// Снимок кеша в порядке, заданном в Servers.json
    public IReadOnlyList<ServerCacheEntry> GetSnapshot()
    {
        var list = _config.Servers?.List;
        if (list == null || list.Count == 0) return Array.Empty<ServerCacheEntry>();

        var result = new List<ServerCacheEntry>(list.Count);
        lock (_cacheLock)
        {
            foreach (var s in list)
            {
                if (_serverCache.TryGetValue((s.Ip, s.Port), out var entry))
                    result.Add(entry);
            }
        }

        return result;
    }

    /// Запуск фонового прохода по всем серверам. Возврат мгновенный.
    private void RefreshAsync(bool force, string reason)
    {
        if (_stopped || !Enabled) return;

        // Уже идёт проход — второй не нужен
        if (Interlocked.CompareExchange(ref _queryInFlight, 1, 0) != 0)
        {
            _logger.Debug($"[ServerStatus] {reason} skipped: query already in flight");
            return;
        }

        // Снимок конфига делаем здесь (главный поток), в фон уходят только копии значений
        var servers = _config.Servers!.List.ToList();
        var timeoutMs = TimeoutMs;
        var ttlSeconds = CacheTtlSeconds;

        _ = Task.Run(async () =>
        {
            try
            {
                var now = DateTime.UtcNow;
                var toQuery = new List<ServerData>(servers.Count);

                foreach (var s in servers)
                {
                    if (force || ttlSeconds == 0)
                    {
                        toQuery.Add(s);
                        continue;
                    }

                    bool needQuery;
                    lock (_cacheLock)
                    {
                        needQuery = !_serverCache.TryGetValue((s.Ip, s.Port), out var entry) ||
                                    (now - entry.UpdatedAtUtc).TotalSeconds >= ttlSeconds;
                    }

                    if (needQuery) toQuery.Add(s);
                }

                if (toQuery.Count == 0)
                {
                    BgDebug($"[ServerStatus] {reason}: cache still fresh, nothing to query");
                    return;
                }

                // Опрашиваем параллельно, но пачками: длинный список серверов иначе поднял бы
                // столько же UDP-сокетов разом. Главный поток при этом не задет в любом случае.
                for (var i = 0; i < toQuery.Count; i += MaxConcurrentQueries)
                {
                    var batch = toQuery.GetRange(i, Math.Min(MaxConcurrentQueries, toQuery.Count - i));
                    await Task.WhenAll(batch.Select(server => QueryAndStoreAsync(server, timeoutMs)))
                        .ConfigureAwait(false);
                }

                int online = 0, offline = 0;
                lock (_cacheLock)
                {
                    foreach (var entry in _serverCache.Values)
                    {
                        if (entry.Online) online++;
                        else offline++;
                    }
                }

                BgDebug(
                    $"[ServerStatus] {reason} completed for {toQuery.Count} server(s): {online} online, {offline} offline");
            }
            catch (Exception ex)
            {
                BgError($"[ServerStatus] {reason} failed", ex);
            }
            finally
            {
                Interlocked.Exchange(ref _queryInFlight, 0);
            }
        });
    }

    private async Task QueryAndStoreAsync(ServerData serverInfo, int timeoutMs)
    {
        A2SInfoResponse? info = null;
        try
        {
            using var cts = new CancellationTokenSource(timeoutMs + 250);
            info = await AdvancedA2S.GetServerInfoAsync(serverInfo.Ip, (ushort)serverInfo.Port, timeoutMs, cts.Token)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            BgDebug($"[ServerStatus] Query failed for {serverInfo.Ip}:{serverInfo.Port} - {ex.Message}");
        }

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

        BgDebug($"[ServerStatus] {serverInfo.Ip}:{serverInfo.Port} - {(info != null ? "ONLINE" : "OFFLINE")}");
    }

    // Всё, что пришло по сети от чужого сервера, — недоверенный текст. Длиннее этого
    // имя карты не бывает, а всё, что длиннее, — попытка забить чат.
    private const int MaxRemoteTextLength = 64;

    /// Чистит строку из A2S-ответа перед подстановкой в шаблон.
    ///
    /// Чужой сервер отвечает тем, чем захочет. Фигурные скобки в его имени карты наш
    /// MessageProcessor принял бы за свои теги ({prefix}, {RED}) — и чужой админ красил бы
    /// наш чат; управляющие символы и переносы строк дали бы многострочный спам.
    /// Убираем и то, и другое, длину ограничиваем.
    internal static string SanitizeRemoteText(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var builder = new System.Text.StringBuilder(Math.Min(value.Length, MaxRemoteTextLength));
        foreach (var c in value)
        {
            if (builder.Length >= MaxRemoteTextLength) break;
            if (char.IsControl(c) || c == '{' || c == '}' || c == '\u2029') continue;
            builder.Append(c);
        }

        return builder.ToString().Trim();
    }

    internal static (string chat, string console) BuildServerLines(ServerData s, A2SInfoResponse? info)
    {
        var map = info == null ? "OFFLINE" : SanitizeRemoteText(info.Map);
        if (map.Length == 0) map = "?";
        string players = info != null ? Math.Max(info.Players - info.Bots, 0).ToString(CultureInfo.InvariantCulture) : "0";
        string max = info?.MaxPlayers.ToString(CultureInfo.InvariantCulture) ?? s.MaxPlayersFallback?.ToString(CultureInfo.InvariantCulture) ?? "?";

        var template = s.MessageTemplate?.Length > 0
            ? s.MessageTemplate
            : "{SERVER_IP}:{SERVER_PORT} - {SERVER_MAP} | {SERVER_PLAYERS}/{SERVER_MAXPLAYERS}";

        var msgChat = Fill(template, s, map, players, max);
        var msgConsole = Fill(s.MessageTemplateConsole?.Length > 0 ? s.MessageTemplateConsole : template,
            s, map, players, max);

        return (msgChat, msgConsole);
    }

    private static string Fill(string template, ServerData s, string map, string players, string max) =>
        template
            .Replace("{SERVER_IP}", s.Ip)
            .Replace("{SERVER_PORT}", s.Port.ToString(CultureInfo.InvariantCulture))
            .Replace("{SERVER_MAP}", map)
            .Replace("{SERVER_PLAYERS}", players)
            .Replace("{SERVER_MAXPLAYERS}", max);

    /// Объявить список серверов игроку, используя текущий снимок кеша.
    /// Вызывать только из главного потока.
    /// Шаблоны из кеша отдаются в print как есть: локализацию, подстановку и рендер
    /// делает DisplayService.Print. Раньше строка обрабатывалась дважды.
    /// print уже привязан к получателю — сервис не знает, что такое игрок.
    public void AnnounceToPlayer(string playerName, Action<MessageType, string> print)
    {
        if (!Enabled)
        {
            _logger.Debug("[ServerStatus] AnnounceToPlayer called but servers disabled or empty");
            return;
        }

        _logger.Debug($"[ServerStatus] Showing server list to {playerName}");

        var snapshot = GetSnapshot();
        _logger.Debug($"[ServerStatus] Cache snapshot contains {snapshot.Count} server(s)");

        if (snapshot.Count == 0)
        {
            _logger.Debug("[ServerStatus] Cache is empty, servers may not have been queried yet");
            return;
        }

        if (!string.IsNullOrEmpty(_config.TitleAnnounceServers))
            print(MessageType.Chat, _config.TitleAnnounceServers!);

        foreach (var entry in snapshot)
        {
            if (!string.IsNullOrEmpty(entry.Chat))
                print(MessageType.Chat, entry.Chat);
        }

        foreach (var entry in snapshot)
        {
            if (!string.IsNullOrEmpty(entry.Console))
                print(MessageType.Console, entry.Console);
        }

        _logger.Debug("[ServerStatus] Finished showing server list");
    }
}
