using System;
using System.Collections.Generic;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;

namespace NotifyMessages;

/// Игровые события, связанные со сменой команд
public sealed partial class NotifyMessages
{
    private HookResult OnPlayerTeam(EventPlayerTeam ev)
    {
        if (ev.IsBot) return HookResult.Continue;

        var player = ev.UserIdPlayer;
        if (player is null || player.IsFakeClient || !player.IsValid) return HookResult.Continue;

        var newTeam = ev.Team;
        var oldTeam = ev.OldTeam;

        if (Config.Debug)
            _logger.Info($"[EVENT] {player.Name} team change: {GetTeamName(oldTeam)} -> {GetTeamName(newTeam)}");

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

    private static string GetTeamName(int team)
    {
        return team switch
        {
            0 => "None",
            1 => "Spectators",
            2 => "Terrorists",
            3 => "Counter-Terrorists",
            _ => $"Unknown({team})"
        };
    }

    private static string ColoredTeam(int team) => team switch
    {
        2 => "{RED}Terrorists{DEFAULT}",
        3 => "{BLUE}Counter-Terrorists{DEFAULT}",
        _ => "{GREY}Spectators{DEFAULT}"
    };

    private void AnnounceTeamChange(IPlayer player, int oldTeam, int newTeam)
    {
        if (string.IsNullOrEmpty(Config.ChangeTeamMessage)) return;

        // Значения уходят в ProcessMessage и подставляются ДО рендера — иначе игрок
        // увидел бы в чате литеральное «{RED}Terrorists{DEFAULT}».
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{PLAYERNAME}"] = player.Name,
            ["{TEAM}"] = ColoredTeam(newTeam),
            ["{OLD_TEAM}"] = ColoredTeam(oldTeam)
        };

        Broadcast(Config.ChangeTeamMessage, values);
    }

    private void AnnouncePlayerTeamJoin(IPlayer player, int team)
    {
        if (string.IsNullOrEmpty(Config.JoinTeamMessage)) return;

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{PLAYERNAME}"] = player.Name,
            ["{TEAM}"] = ColoredTeam(team)
        };

        Broadcast(Config.JoinTeamMessage, values);
    }

    /// Каждому игроку — на его языке: локализация идёт по SteamID получателя.
    private void Broadcast(string template, IReadOnlyDictionary<string, string> values)
    {
        foreach (var p in Core.PlayerManager.GetAllPlayers())
        {
            if (p.IsFakeClient || !p.IsValid) continue;
            _displayService.Print(MessageType.Chat, template, p, values);
        }
    }
}
