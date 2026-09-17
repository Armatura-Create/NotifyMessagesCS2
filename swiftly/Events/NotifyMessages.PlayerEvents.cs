using System;
using System.Collections.Generic;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;

namespace NotifyMessages;

/// Игровые события, связанные с подключением игроков
public sealed partial class NotifyMessages
{
    private HookResult OnPlayerDisconnect(EventPlayerDisconnect ev)
    {
        _logger.Debug("[LEAVE] 1/4 Disconnect: событие получено");

        // SteamID и ник берём из полей события, а не с контроллера: на выходе игрока
        // контроллер уже может быть частично разобран, а поля события — обычные значения,
        // скопированные движком в payload.
        var steamId = ev.XuID;
        var playerName = ev.Name ?? "";

        if (steamId == 0)
        {
            var player = ev.UserIdPlayer;
            if (player is null || player.IsFakeClient) return HookResult.Continue;

            try
            {
                _logger.Debug("[LEAVE] 2/4 читаю SteamID и ник с разбираемого контроллера");
                steamId = player.SteamID;
                if (playerName.Length == 0) playerName = player.Name;
            }
            catch (Exception ex)
            {
                _logger.Debug($"[EVENT] Не удалось прочитать данные отключившегося игрока: {ex.Message}");
                return HookResult.Continue;
            }
        }

        _logger.Debug($"[LEAVE] 3/4 игрок {playerName} (SteamID {steamId}) отключился");

        if (_sessionService.TryKillAndRemoveConnectionTimer(steamId))
        {
            _logger.Debug($"  -> Killed connection timer for {playerName}");
        }
        else if (_sessionService.IsFullyConnected(steamId) && Config.LeaveMessages != null)
        {
            _geoIpService.TryGetPlayerIso(steamId, out var country);
            _geoIpService.TryGetPlayerCity(steamId, out var city);

            var values = PlayerValues(playerName, country, city);

            foreach (var p in Core.PlayerManager.GetAllPlayers())
            {
                if (p.IsFakeClient || !p.IsValid || p.SteamID == steamId) continue;

                var template = _messageProcessor.GetRandomLocalizedMessage(Config.LeaveMessages, p.SteamID);

                if (!string.IsNullOrEmpty(template))
                    _displayService.Print(MessageType.Chat, template, p, values);
            }
        }

        _logger.Debug($"[LEAVE] 4/4 чищу состояние игрока {playerName}");

        _sessionService.RemoveFullyConnected(steamId);
        _sessionService.RemoveLanguage(steamId);
        _geoIpService.RemovePlayer(steamId);
        _serversCommandCooldown.Remove(steamId); // иначе словарь растёт всё время жизни сервера

        _logger.Debug("[LEAVE] Disconnect завершён");
        return HookResult.Continue;
    }

    /// Кеширует страну и город игрока по его IP.
    /// Игрок приходит СВЕРХУ — из события или из PlayerManager, — и никогда не добывается
    /// по номеру слота: см. историю краша в cssharp/ и в CLAUDE.md.
    private void CachePlayerGeo(IPlayer player, ulong steamId)
    {
        string ip;
        try
        {
            _logger.Debug("[JOIN] 4/8 читаю player.IPAddress");
            ip = GeoIpService.ExtractIp(player.IPAddress);
        }
        catch (Exception ex)
        {
            _logger.Debug($"[GeoIP] Не удалось получить IP игрока {steamId}: {ex.Message}");
            return;
        }

        if (string.IsNullOrEmpty(ip))
        {
            _logger.Debug("[JOIN] 4/8 IP пуст, гео пропущено");
            return;
        }

        var defaultLang = Config.DefaultLang ?? string.Empty;
        _geoIpService.UpdatePlayerCache(steamId, ip, defaultLang);
    }

    /// Двухбуквенный код языка ИНТЕРФЕЙСА ИГРЫ игрока ("ru", "en") или null — как в SourceMod.
    ///
    /// Источник — userinfo-квар cl_language, прочитанный синхронно через GetClientConvarValue
    /// и переведённый в код таблицей SteamLanguage. НЕ player.PlayerLanguage: SwiftlyS2
    /// строит его из того же квара, но асинхронно (QueryClientConvar при OnClientPutInServer)
    /// и до прихода ответа отдаёт язык СЕРВЕРА из core.jsonc. player_connect_full успевает
    /// раньше ответа, и снимок на входе получал серверный "en" у всех игроков.
    ///
    /// PlayerLanguage остаётся фолбэком на случай, если userinfo пуст: к моменту, когда
    /// он понадобится (приветствие через DisplayDelay), ответ на квар уже пришёл.
    private string? ReadClientLanguage(IPlayer player)
    {
        try
        {
            var raw = player.GetClientConvarValue("cl_language");
            var language = SteamLanguage.ToCode(raw);
            if (language != null)
            {
                _logger.Debug($"[JOIN] 5/8 язык интерфейса игры: {raw} -> {language}");
                return language;
            }

            var fallback = SteamLanguage.ToCode(player.PlayerLanguage.Value);
            _logger.Debug($"[JOIN] 5/8 cl_language пуст/неизвестен ({raw ?? "null"}), " +
                          $"PlayerLanguage SwiftlyS2: {fallback ?? "неизвестен"}");
            return fallback;
        }
        catch (Exception ex)
        {
            _logger.Debug($"[Lang] Не удалось прочитать язык клиента: {ex.Message}");
            return null;
        }
    }

    /// Контекстные значения игрока для подстановки в шаблон.
    /// Одна точка вместо россыпи .Replace по вызывающему коду.
    private static Dictionary<string, string> PlayerValues(string playerName, string? country, string? city)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["{PLAYERNAME}"] = playerName,
            ["{COUNTRY}"] = country ?? "Unknown",
            ["{CITY}"] = city ?? "Unknown"
        };

    /// Игрока ищем заново по SteamID: между постановкой таймера и его срабатыванием
    /// игрок мог выйти, а объект — освободиться.
    private IPlayer? FindConnectedPlayer(ulong steamId)
    {
        try
        {
            var player = Core.PlayerManager.GetPlayerFromSteamId(steamId, allowUnauthorized: false);
            return player is { IsValid: true, IsFakeClient: false } ? player : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private HookResult OnPlayerConnectFull(EventPlayerConnectFull ev)
    {
        // Трассировка пути подключения — см. cssharp/: каждый шаг логируется ПЕРЕД опасной
        // операцией, и последняя строка в логе называет то, на чём умер процесс.
        _logger.Debug("[JOIN] 1/8 ConnectFull: событие получено, проверяю игрока");

        var player = ev.UserIdPlayer;
        if (player is null || !player.IsValid || player.IsFakeClient)
        {
            _logger.Debug("[JOIN] -- пропуск: игрок null/невалиден/бот");
            return HookResult.Continue;
        }

        _logger.Debug("[JOIN] 2/8 читаю SteamID и ник");

        // Снимаем значения ДО таймеров: через 3 секунды игрок может быть уже невалиден.
        var steamId = player.SteamID;
        var playerName = player.Name;

        _logger.Debug($"[JOIN] 3/8 игрок {playerName} (SteamID {steamId}), читаю IP и гео");

        CachePlayerGeo(player, steamId);

        _logger.Debug("[JOIN] 5/8 читаю язык клиента (PlayerLanguage)");
        _sessionService.SetLanguage(steamId, ReadClientLanguage(player));

        // В CS2 при смене карты player_disconnect не приходит, а player_connect_full приходит
        // заново. Игрок, который уже числится fully-connected, — это возврат на новую карту,
        // а не заход: без анонса, без приветствия, и его первое попадание в команду —
        // тоже не событие (см. TeamEvents). Гео и язык при этом обновлены выше.
        var returning = _sessionService.IsFullyConnected(steamId);

        _logger.Debug(returning
            ? "[JOIN] 6/8 игрок уже был на сервере — возврат после смены карты"
            : "[JOIN] 6/8 регистрирую сессию");
        _sessionService.AddFullyConnected(steamId);
        _sessionService.TryKillAndRemoveConnectionTimer(steamId);

        if (returning)
        {
            _sessionService.MarkReturning(steamId, DateTime.UtcNow);
            _logger.Debug("[JOIN] 8/8 смена карты: анонс входа и приветствие пропущены, ConnectFull завершён");
            return HookResult.Continue;
        }

        _logger.Debug($"[JOIN] 7/8 ставлю таймер анонса входа ({JoinAnnounceDelaySeconds} с)");

        _sessionService.SetConnectionTimer(steamId, DelayOnce(JoinAnnounceDelaySeconds, () =>
        {
            _logger.Debug($"[JOIN-TIMER] сработал для {playerName}");

            if (Config.JoinMessages != null)
            {
                _geoIpService.TryGetPlayerIso(steamId, out var country);
                _geoIpService.TryGetPlayerCity(steamId, out var city);

                _logger.Debug($"[JOIN-TIMER] гео из кеша: {city ?? "Unknown"}, {country ?? "Unknown"}; рассылаю анонс");

                var values = PlayerValues(playerName, country, city);

                foreach (var p in Core.PlayerManager.GetAllPlayers())
                {
                    if (p.IsFakeClient || !p.IsValid) continue;

                    var template = _messageProcessor.GetRandomLocalizedMessage(Config.JoinMessages, p.SteamID);
                    if (!string.IsNullOrEmpty(template))
                        _displayService.Print(MessageType.Chat, template, p, values);
                }
            }

            _sessionService.RemoveConnectionTimer(steamId);
            _logger.Debug("[JOIN-TIMER] анонс входа завершён");
        }));

        var welcome = Config.WelcomeMessage;
        if (welcome == null || string.IsNullOrEmpty(welcome.Message))
        {
            _logger.Debug("[JOIN] 8/8 приветствие не настроено, ConnectFull завершён");
            return HookResult.Continue;
        }

        _logger.Debug($"[JOIN] 8/8 ставлю таймер приветствия ({welcome.DisplayDelay} с, канал {welcome.MessageType}), ConnectFull завершён");

        var template = welcome.Message;
        var welcomeValues = PlayerValues(playerName, null, null);

        // IPlayer через таймер НЕ проносим: за DisplayDelay игрок может выйти.
        // Ищем игрока заново по SteamID.
        DelayOnce(welcome.DisplayDelay, () =>
        {
            _logger.Debug($"[WELCOME-TIMER] сработал, ищу игрока {steamId}");

            var target = FindConnectedPlayer(steamId);
            if (target == null)
            {
                _logger.Debug("[WELCOME-TIMER] игрок уже вышел, приветствие пропущено");
                return;
            }

            _logger.Debug($"[WELCOME-TIMER] показываю приветствие в {welcome.MessageType}");
            _displayService.Print(welcome.MessageType, template, target, welcomeValues);
            _logger.Debug("[WELCOME-TIMER] приветствие показано");
        });

        return HookResult.Continue;
    }
}
