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

        if (_connectionTimers.TryGetValue(player.SteamID, out var value))
        {
            value.Kill();
            _connectionTimers.Remove(player.SteamID);
        }
        else if (_fullyConnectedPlayers.Contains(player.SteamID))
        {
            if (Config.LeaveMessages != null)
            {
                _playerIsoCode.TryGetValue(player.SteamID, out var country);
                _playerCity.TryGetValue(player.SteamID, out var city);

                foreach (var p in Utilities.GetPlayers()
                             .Where(u => u is { IsBot: false, IsValid: true } && u.SteamID != player.SteamID))
                {
                    var message = GetRandomLocalizedMessage(Config.LeaveMessages, p.SteamID, player.PlayerName,
                        country ?? "Unknown", city ?? "Unknown");

                    if (!string.IsNullOrEmpty(message))
                    {
                        PrintWrappedLine(HudDestination.Chat, message, p, true);
                    }
                }
            }
        }

        _fullyConnectedPlayers.Remove(player.SteamID);
        _playerIsoCode.Remove(player.SteamID);
        _playerCity.Remove(player.SteamID);

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
        _users[slot] = new User();

        if (player?.IpAddress == null) return;

        var ip = player.IpAddress.Split(':')[0];
        var defaultLang = Config.DefaultLang ?? string.Empty;
        _playerIsoCode.TryAdd(id.SteamId64, _geoIpService.GetIsoCode(ip, defaultLang));
        _playerCity.TryAdd(id.SteamId64, _geoIpService.GetCity(ip));
    }

    private HookResult EventPlayerConnectFull(EventPlayerConnectFull ev, GameEventInfo info)
    {
        var player = ev.Userid;
        if (player is null || !player.IsValid || player.IsBot)
            return HookResult.Continue;

        _fullyConnectedPlayers.Add(player.SteamID);

        if (_connectionTimers.ContainsKey(player.SteamID))
        {
            _connectionTimers[player.SteamID].Kill();
            _connectionTimers.Remove(player.SteamID);
        }

        _connectionTimers[player.SteamID] = AddTimer(3.0f, () =>
        {
            if (Config.JoinMessages != null)
            {
                if (!player.IsValid) return;

                _playerIsoCode.TryGetValue(player.SteamID, out var country);
                _playerCity.TryGetValue(player.SteamID, out var city);

                foreach (var p in Utilities.GetPlayers().Where(u => u is { IsBot: false, IsValid: true }))
                {
                    var message = GetRandomLocalizedMessage(Config.JoinMessages, p.SteamID, player.PlayerName,
                        country ?? "Unknown", city ?? "Unknown");
                    if (!string.IsNullOrEmpty(message))
                        PrintWrappedLine(HudDestination.Chat, message, p, true);
                }
            }

            _connectionTimers.Remove(player.SteamID);
        });

        if (Config.WelcomeMessage == null || string.IsNullOrEmpty(Config.WelcomeMessage.Message))
            return HookResult.Continue;

        var welcomeMsg = Config.WelcomeMessage;
        var msg = welcomeMsg.Message.Replace("{PLAYERNAME}", player.PlayerName);
        HudDestination type = Config.WelcomeMessage.MessageType == 0 ? HudDestination.Chat : HudDestination.Center;

        AddTimer(Config.WelcomeMessage.DisplayDelay, () => { PrintWrappedLine(type, msg, player, true); });

        return HookResult.Continue;
    }
}
