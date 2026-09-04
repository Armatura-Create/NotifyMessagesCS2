using System;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;

namespace NotifyMessages;

/// Игровые события, связанные с подключением игроков
public partial class NotifyMessages
{
    // --- События игрока ---
    private HookResult EventPlayerDisconnect(EventPlayerDisconnect ev, GameEventInfo info)
    {
        _logger.Debug("[LEAVE] 1/4 Disconnect: событие получено");

        var player = ev.Userid;
        // IsValid здесь НЕ проверяем: контроллер уже может быть частично разобран,
        // но почистить сессию и гео-кеш всё равно обязаны
        if (player is null || player.IsBot) return HookResult.Continue;

        // Значения снимаем сразу: GameEvent живёт только внутри обработчика.
        // Slot здесь намеренно НЕ читаем: контроллер уже разбирается, а HTML-слот
        // гасится в OnTick по сверке SteamID — это надёжнее, чем ловить момент отключения.
        // Само чтение полей тоже защищаем: на этом этапе сущность уже может быть невалидна.
        ulong steamId;
        string playerName;
        try
        {
            _logger.Debug("[LEAVE] 2/4 читаю SteamID и ник с разбираемого контроллера");
            steamId = player.SteamID;
            playerName = player.PlayerName;
        }
        catch (Exception ex)
        {
            _logger.Debug($"[EVENT] Не удалось прочитать данные отключившегося игрока: {ex.Message}");
            return HookResult.Continue;
        }

        _logger.Debug($"[LEAVE] 3/4 игрок {playerName} (SteamID {steamId}) отключился");

        if (_sessionService.TryKillAndRemoveConnectionTimer(steamId))
        {
            if (Config.Debug)
                _logger.Debug($"  -> Killed connection timer for {playerName}");
        }
        else if (_sessionService.IsFullyConnected(steamId) && Config.LeaveMessages != null)
        {
            _geoIpService.TryGetPlayerIso(steamId, out var country);
            _geoIpService.TryGetPlayerCity(steamId, out var city);

            var values = PlayerValues(playerName, country, city);

            foreach (var p in Utilities.GetPlayers()
                         .Where(u => u is { IsBot: false, IsValid: true } && u.SteamID != steamId))
            {
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

    private static HookResult EventPlayerDisconnectPre(EventPlayerDisconnect ev, GameEventInfo info)
    {
        info.DontBroadcast = true;
        return HookResult.Continue;
    }

    /// Кеширует страну и город игрока по его IP.
    ///
    /// Контроллер приходит СВЕРХУ — из события или из Utilities.GetPlayers(), — и никогда
    /// не добывается по номеру слота. Utilities.GetPlayerFromSlot(slot) внутри делает
    /// new CCSPlayerController(EntitySystem.GetEntityByIndex(slot + 1)) без проверки типа
    /// сущности: на авторизации Steam контроллера в слоте может ещё не быть, и чтение
    /// IpAddress уходит по смещениям чужой энтити — сервер падает без записи в лог,
    /// а IsValid этого не ловит, потому что проверяет указатель, а не тип.
    private void CachePlayerGeo(CCSPlayerController player, ulong steamId)
    {
        string ip;
        try
        {
            _logger.Debug("[JOIN] 4/8 читаю player.IpAddress");
            // IpAddress бросает InvalidOperationException, если сущность уже невалидна
            ip = GeoIpService.ExtractIp(player.IpAddress);
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

    /// Двухбуквенный код языка клиента ("ru", "en") или null.
    /// Нативы бросают на невалидной сущности — один игрок не должен ронять обработчик.
    private string? ReadClientLanguage(CCSPlayerController player)
    {
        try
        {
            var language = player.GetLanguage()?.TwoLetterISOLanguageName;
            _logger.Debug($"[JOIN] 5/8 язык клиента: {language ?? "неизвестен"}");
            return language;
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

    /// Возвращает подключённого игрока по SteamID или null.
    /// Только через Utilities.GetPlayers() — он фильтрует по IsValid и Connected.
    private static CCSPlayerController? FindConnectedPlayer(ulong steamId)
    {
        foreach (var p in Utilities.GetPlayers())
        {
            if (!p.IsBot && p.SteamID == steamId) return p;
        }

        return null;
    }

    private HookResult EventPlayerConnectFull(EventPlayerConnectFull ev, GameEventInfo info)
    {
        // Трассировка пути подключения.
        //
        // Нарушение памяти в нативном слое убивает процесс без исключения и без стека:
        // единственное, что остаётся, — последняя успевшая напечататься строка. Поэтому
        // каждый шаг логируется ПЕРЕД опасной операцией, а не после: последняя строка в логе
        // называет то, на чём сервер умер. Всё под Debug — включается Settings.json.
        _logger.Debug("[JOIN] 1/8 ConnectFull: событие получено, проверяю контроллер");

        var player = ev.Userid;
        if (player is null || !player.IsValid || player.IsBot)
        {
            _logger.Debug("[JOIN] -- пропуск: контроллер null/невалиден/бот");
            return HookResult.Continue;
        }

        _logger.Debug("[JOIN] 2/8 читаю SteamID и ник с контроллера");

        // Снимаем значения ДО таймеров: через 3 секунды контроллер может быть уже невалиден,
        // и обращение к player.SteamID из колбэка роняло плагин.
        var steamId = player.SteamID;
        var playerName = player.PlayerName;

        _logger.Debug($"[JOIN] 3/8 игрок {playerName} (SteamID {steamId}), читаю IP и гео");

        // Гео и язык снимаем здесь, а не на авторизации Steam: контроллер пришёл из события,
        // значит сущность существует и её тип верный.
        CachePlayerGeo(player, steamId);

        _logger.Debug("[JOIN] 5/8 читаю язык клиента (GetLanguage)");
        _sessionService.SetLanguage(steamId, ReadClientLanguage(player));

        _logger.Debug("[JOIN] 6/8 регистрирую сессию");
        _sessionService.AddFullyConnected(steamId);
        _sessionService.TryKillAndRemoveConnectionTimer(steamId);

        _logger.Debug($"[JOIN] 7/8 ставлю таймер анонса входа ({JoinAnnounceDelaySeconds} с)");

        _sessionService.SetConnectionTimer(steamId, AddTimer(JoinAnnounceDelaySeconds, () =>
        {
            _logger.Debug($"[JOIN-TIMER] сработал для {playerName}");

            if (Config.JoinMessages != null)
            {
                _geoIpService.TryGetPlayerIso(steamId, out var country);
                _geoIpService.TryGetPlayerCity(steamId, out var city);

                _logger.Debug($"[JOIN-TIMER] гео из кеша: {city ?? "Unknown"}, {country ?? "Unknown"}; рассылаю анонс");

                var values = PlayerValues(playerName, country, city);

                foreach (var p in Utilities.GetPlayers().Where(u => u is { IsBot: false, IsValid: true }))
                {
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

        // Контроллер через таймер НЕ проносим: за DisplayDelay игрок может выйти, объект
        // будет освобождён, а обращение к нему (даже к IsValid) — это чтение чужой памяти
        // и краш сервера без единой строки в логе. Ищем игрока заново по SteamID.
        AddTimer(welcome.DisplayDelay, () =>
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
