using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace NotifyMessages;

/// Игровые события, связанные со сменой команд
public partial class NotifyMessages
{
    private HookResult EventPlayerTeamChange(EventPlayerTeam ev, GameEventInfo info)
    {
        var player = ev.Userid;
        if (player is null || player.IsBot || !player.IsValid) return HookResult.Continue;

        var newTeam = ev.Team;
        var oldTeam = ev.Oldteam;

        if (oldTeam == 0 && (newTeam == 1 || newTeam == 2 || newTeam == 3))
        {
            AnnouncePlayerTeamJoin(player, newTeam);
            return HookResult.Continue;
        }

        if (newTeam != 0 && newTeam != oldTeam)
        {
            AnnounceTeamChange(player, oldTeam, newTeam);
        }

        return HookResult.Continue;
    }

    private void AnnounceTeamChange(CCSPlayerController player, int oldTeam, int newTeam)
    {
        if (string.IsNullOrEmpty(Config.ChangeTeamMessage)) return;

        var playerName = player.PlayerName;

        var teamName = newTeam switch
        {
            2 => "{RED}Terrorists{DEFAULT}",
            3 => "{BLUE}Counter-Terrorists{DEFAULT}",
            _ => "{GREY}Spectators{DEFAULT}"
        };

        var oldTeamName = oldTeam switch
        {
            2 => "{RED}Terrorists{DEFAULT}",
            3 => "{BLUE}Counter-Terrorists{DEFAULT}",
            _ => "{GREY}Spectators{DEFAULT}"
        };

        foreach (var p in Utilities.GetPlayers().Where(u => u is { IsBot: false, IsValid: true }))
        {
            // Используем steamID каждого игрока для локализации сообщения
            var msg = _messageProcessor.ProcessMessage(Config.ChangeTeamMessage, p.SteamID)
                .Replace("{PLAYERNAME}", playerName)
                .Replace("{TEAM}", teamName)
                .Replace("{OLD_TEAM}", oldTeamName);

            _displayService.Print(HudDestination.Chat, msg, p, true);
        }
    }

    private void AnnouncePlayerTeamJoin(CCSPlayerController player, int team)
    {
        if (string.IsNullOrEmpty(Config.JoinTeamMessage)) return;

        var playerName = player.PlayerName;

        var teamName = team switch
        {
            2 => "{RED}Terrorists{DEFAULT}",
            3 => "{BLUE}Counter-Terrorists{DEFAULT}",
            _ => "{GREY}Spectators{DEFAULT}"
        };

        foreach (var p in Utilities.GetPlayers().Where(u => u is { IsBot: false, IsValid: true }))
        {
            // Используем steamID каждого игрока для локализации сообщения
            var msg = _messageProcessor.ProcessMessage(Config.JoinTeamMessage, p.SteamID)
                .Replace("{PLAYERNAME}", playerName)
                .Replace("{TEAM}", teamName);

            _displayService.Print(HudDestination.Chat, msg, p, true);
        }
    }

    private HookResult EventPlayerTeamChangePre(EventPlayerTeam ev, GameEventInfo info)
    {
        info.DontBroadcast = true;
        return HookResult.Continue;
    }
}
