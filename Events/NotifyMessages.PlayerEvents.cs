using System;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Entities;

namespace NotifyMessages;

/// Игровые события, связанные с подключением игроков
public partial class NotifyMessages
{
    // --- События игрока ---
    private HookResult EventPlayerDisconnect(EventPlayerDisconnect ev, GameEventInfo info)
    {
        var player = ev.Userid;
        // IsValid здесь НЕ проверяем: контроллер уже может быть частично разобран,
        // но почистить сессию и гео-кеш всё равно обязаны
        if (player is null || player.IsBot) return HookResult.Continue;

        // Значения снимаем сразу: GameEvent живёт только внутри обработчика.
        // Slot здесь намеренно НЕ читаем: контроллер уже разбирается, а HTML-слот
        // гасится в OnTick по сверке SteamID — это надёжнее, чем ловить момент отключения.
        var steamId = player.SteamID;
        var playerName = player.PlayerName;

        if (Config.Debug)
            _logger.Info($"[EVENT] Player disconnected: {playerName} (SteamID: {steamId})");

        if (_sessionService.TryKillAndRemoveConnectionTimer(steamId))
        {
            if (Config.Debug)
                _logger.Debug($"  -> Killed connection timer for {playerName}");
        }
        else if (_sessionService.IsFullyConnected(steamId) && Config.LeaveMessages != null)
        {
            _geoIpService.TryGetPlayerIso(steamId, out var country);
            _geoIpService.TryGetPlayerCity(steamId, out var city);

            foreach (var p in Utilities.GetPlayers()
                         .Where(u => u is { IsBot: false, IsValid: true } && u.SteamID != steamId))
            {
                var message = _messageProcessor.GetRandomLocalizedMessage(Config.LeaveMessages, p.SteamID, playerName,
                    country ?? "Unknown", city ?? "Unknown");

                if (!string.IsNullOrEmpty(message))
                    _displayService.Print(HudDestination.Chat, message, p);
            }
        }

        _sessionService.RemoveFullyConnected(steamId);
        _geoIpService.RemovePlayer(steamId);

        return HookResult.Continue;
    }

    private HookResult EventPlayerDisconnectPre(EventPlayerDisconnect ev, GameEventInfo info)
    {
        info.DontBroadcast = true;
        return HookResult.Continue;
    }

    private void OnClientAuthorized(int slot, SteamID id)
    {
        // GetPlayerFromSlot оборачивает в CCSPlayerController любую сущность с таким
        // индексом, не проверяя тип, поэтому валидность проверяем сами.
        var player = Utilities.GetPlayerFromSlot(slot);
        if (player is not { IsValid: true }) return;

        string ip;
        try
        {
            // IpAddress бросает InvalidOperationException, если сущность уже невалидна
            ip = GeoIpService.ExtractIp(player.IpAddress);
        }
        catch (Exception ex)
        {
            _logger.Debug($"[GeoIP] Не удалось получить IP для slot {slot}: {ex.Message}");
            return;
        }

        if (string.IsNullOrEmpty(ip)) return;

        var defaultLang = Config.DefaultLang ?? string.Empty;
        _geoIpService.UpdatePlayerCache(id.SteamId64, ip, defaultLang);
    }

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
        var player = ev.Userid;
        if (player is null || !player.IsValid || player.IsBot)
            return HookResult.Continue;

        // Снимаем значения ДО таймеров: через 3 секунды контроллер может быть уже невалиден,
        // и обращение к player.SteamID из колбэка роняло плагин.
        var steamId = player.SteamID;
        var playerName = player.PlayerName;

        if (Config.Debug)
            _logger.Info($"[EVENT] Player connected: {playerName} (SteamID: {steamId})");

        _sessionService.AddFullyConnected(steamId);
        _sessionService.TryKillAndRemoveConnectionTimer(steamId);

        _sessionService.SetConnectionTimer(steamId, AddTimer(JoinAnnounceDelaySeconds, () =>
        {
            if (Config.JoinMessages != null)
            {
                _geoIpService.TryGetPlayerIso(steamId, out var country);
                _geoIpService.TryGetPlayerCity(steamId, out var city);

                if (Config.Debug)
                    _logger.Info($"[GeoIP] {playerName} location: {city ?? "Unknown"}, {country ?? "Unknown"}");

                foreach (var p in Utilities.GetPlayers().Where(u => u is { IsBot: false, IsValid: true }))
                {
                    var message = _messageProcessor.GetRandomLocalizedMessage(Config.JoinMessages, p.SteamID,
                        playerName, country ?? "Unknown", city ?? "Unknown");
                    if (!string.IsNullOrEmpty(message))
                        _displayService.Print(HudDestination.Chat, message, p);
                }
            }

            _sessionService.RemoveConnectionTimer(steamId);
        }));

        var welcome = Config.WelcomeMessage;
        if (welcome == null || string.IsNullOrEmpty(welcome.Message))
            return HookResult.Continue;

        // Используем MessageProcessor для поддержки всех тегов, включая локализацию
        var msg = _messageProcessor.ProcessMessage(welcome.Message, steamId)
            .Replace("{PLAYERNAME}", playerName);

        // Раньше учитывались только Chat/Center, а Console и Alert молча становились Center
        var destination = DisplayService.ToHudDestination(welcome.MessageType);

        // Контроллер через таймер НЕ проносим: за DisplayDelay игрок может выйти, объект
        // будет освобождён, а обращение к нему (даже к IsValid) — это чтение чужой памяти
        // и краш сервера без единой строки в логе. Ищем игрока заново по SteamID.
        AddTimer(welcome.DisplayDelay, () =>
        {
            var target = FindConnectedPlayer(steamId);
            if (target != null) _displayService.Print(destination, msg, target);
        });

        return HookResult.Continue;
    }
}
