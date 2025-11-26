using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
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
        if (player is null || player.IsBot) return HookResult.Continue;

        if (Config.Debug)
        {
            _logger.Info($"[EVENT] Player disconnected: {player.PlayerName} (SteamID: {player.SteamID})");
        }

        if (_sessionService.TryKillAndRemoveConnectionTimer(player.SteamID))
        {
            if (Config.Debug)
            {
                _logger.Debug($"  ↳ Killed connection timer for {player.PlayerName}");
            }
        }
        else if (_sessionService.IsFullyConnected(player.SteamID))
        {
            if (Config.LeaveMessages != null)
            {
                _geoIpService.TryGetPlayerIso(player.SteamID, out var country);
                _geoIpService.TryGetPlayerCity(player.SteamID, out var city);

                foreach (var p in Utilities.GetPlayers()
                             .Where(u => u is { IsBot: false, IsValid: true } && u.SteamID != player.SteamID))
                {
                    var message = _messageProcessor.GetRandomLocalizedMessage(Config.LeaveMessages, p.SteamID, player.PlayerName,
                        country ?? "Unknown", city ?? "Unknown");

                    if (!string.IsNullOrEmpty(message))
                    {
                        _displayService.Print(HudDestination.Chat, message, p, true);
                    }
                }
            }
        }

        _sessionService.RemoveFullyConnected(player.SteamID);
        _geoIpService.RemovePlayer(player.SteamID);

        return HookResult.Continue;
    }

    private HookResult EventPlayerDisconnectPre(EventPlayerDisconnect ev, GameEventInfo info)
    {
        info.DontBroadcast = true;
        return HookResult.Continue;
    }

    private void OnClientAuthorized(int slot, SteamID id)
    {
        var player = Utilities.GetPlayerFromSlot(slot);

        if (player?.IpAddress == null) return;

        var ip = player.IpAddress.Split(':')[0];
        var defaultLang = Config.DefaultLang ?? string.Empty;
        _geoIpService.UpdatePlayerCache(id.SteamId64, ip, defaultLang);
    }

    private HookResult EventPlayerConnectFull(EventPlayerConnectFull ev, GameEventInfo info)
    {
        var player = ev.Userid;
        if (player is null || !player.IsValid || player.IsBot)
            return HookResult.Continue;

        if (Config.Debug)
        {
            _logger.Info($"[EVENT] Player connected: {player.PlayerName} (SteamID: {player.SteamID})");
        }

        _sessionService.AddFullyConnected(player.SteamID);

        _sessionService.TryKillAndRemoveConnectionTimer(player.SteamID);

        _sessionService.SetConnectionTimer(player.SteamID, AddTimer(3.0f, () =>
        {
            if (Config.JoinMessages != null)
            {
                if (!player.IsValid) return;

                _geoIpService.TryGetPlayerIso(player.SteamID, out var country);
                _geoIpService.TryGetPlayerCity(player.SteamID, out var city);

                if (Config.Debug)
                {
                    _logger.Info($"[GeoIP] {player.PlayerName} location: {city ?? "Unknown"}, {country ?? "Unknown"}");
                }

                foreach (var p in Utilities.GetPlayers().Where(u => u is { IsBot: false, IsValid: true }))
                {
                    var message = _messageProcessor.GetRandomLocalizedMessage(Config.JoinMessages, p.SteamID, player.PlayerName,
                        country ?? "Unknown", city ?? "Unknown");
                    if (!string.IsNullOrEmpty(message))
                        _displayService.Print(HudDestination.Chat, message, p, true);
                }
            }

            _sessionService.RemoveConnectionTimer(player.SteamID);
        }));

        if (Config.WelcomeMessage == null || string.IsNullOrEmpty(Config.WelcomeMessage.Message))
            return HookResult.Continue;

        var welcomeMsg = Config.WelcomeMessage;
        // Используем MessageProcessor для поддержки всех тегов, включая локализацию
        var msg = _messageProcessor.ProcessMessage(welcomeMsg.Message, player.SteamID)
            .Replace("{PLAYERNAME}", player.PlayerName);
        HudDestination type = Config.WelcomeMessage.MessageType == 0 ? HudDestination.Chat : HudDestination.Center;

        AddTimer(Config.WelcomeMessage.DisplayDelay, () => { _displayService.Print(type, msg, player, true); });

        return HookResult.Continue;
    }
}
