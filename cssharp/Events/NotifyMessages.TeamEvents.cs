using System;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace NotifyMessages;

/// Игровые события, связанные со сменой команд
public partial class NotifyMessages
{
    /// Сколько после возврата на новую карту первое попадание в команду считается
    /// не событием. Дольше — игрок сидел в спектаторах и его выход в команду настоящий.
    private static readonly TimeSpan ReturningGrace = TimeSpan.FromSeconds(90);

    // Переходы T<->CT, накопленные за кадр: анонс откладывается на один кадр, чтобы
    // отличить смену сторон (все сразу) от перехода одного игрока. Через границу кадра
    // уходят только строки и числа — контроллер не переживает кадр.
    private readonly List<(string Name, int OldTeam, int NewTeam)> _pendingTeamChanges = new();
    private bool _teamFlushScheduled;

    private HookResult EventPlayerTeamChange(EventPlayerTeam ev, GameEventInfo info)
    {
        var player = ev.Userid;
        if (player is null || player.IsBot || !player.IsValid) return HookResult.Continue;

        var newTeam = ev.Team;
        var oldTeam = ev.Oldteam;
        var playerName = player.PlayerName;
        var steamId = player.SteamID;

        if (Config.Debug)
            _logger.Info($"[EVENT] {playerName} team change: {GetTeamName(oldTeam)} -> {GetTeamName(newTeam)} " +
                         $"(silent={ev.Silent}, disconnect={ev.Disconnect})");

        // Уход с сервера — не смена команды
        if (ev.Disconnect || newTeam == 0 || newTeam == oldTeam) return HookResult.Continue;

        // Движок сам молчит в чате про эту смену (halftime-свап, тихие переводы плагинами) — и мы молчим
        if (ev.Silent)
        {
            _logger.Debug($"[TEAM] {playerName}: silent-смена, анонс пропущен");
            return HookResult.Continue;
        }

        // Первое попадание в команду после смены карты — возврат, а не событие
        if (oldTeam == 0 && _sessionService.TakeReturning(steamId, DateTime.UtcNow, ReturningGrace))
        {
            _logger.Debug($"[TEAM] {playerName}: команда после смены карты, анонс пропущен");
            return HookResult.Continue;
        }

        if (oldTeam == 0)
        {
            AnnouncePlayerTeamJoin(playerName, newTeam);
            return HookResult.Continue;
        }

        // Переход между командами анонсируем через кадр — пачкой, чтобы отсеять смену сторон
        _pendingTeamChanges.Add((playerName, oldTeam, newTeam));

        if (!_teamFlushScheduled)
        {
            _teamFlushScheduled = true;
            Server.NextFrame(FlushTeamChanges);
        }

        return HookResult.Continue;
    }

    private void FlushTeamChanges()
    {
        _teamFlushScheduled = false;

        var batch = _pendingTeamChanges.ToArray();
        _pendingTeamChanges.Clear();
        if (batch.Length == 0) return;

        if (TeamSwap.IsMassSwap(batch.Select(c => (c.OldTeam, c.NewTeam)).ToArray()))
        {
            _logger.Debug($"[TEAM] смена сторон ({batch.Length} игроков за кадр), анонс пропущен");
            return;
        }

        foreach (var (name, oldTeam, newTeam) in batch)
            AnnounceTeamChange(name, oldTeam, newTeam);
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

    private void AnnounceTeamChange(string playerName, int oldTeam, int newTeam)
    {
        if (string.IsNullOrEmpty(Config.ChangeTeamMessage)) return;

        // Значения уходят в ProcessMessage и подставляются ДО рендера. Раньше они
        // подставлялись после него, и игрок видел в чате литеральное «{RED}Terrorists{DEFAULT}».
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{PLAYERNAME}"] = playerName,
            ["{TEAM}"] = ColoredTeam(newTeam),
            ["{OLD_TEAM}"] = ColoredTeam(oldTeam)
        };

        Broadcast(Config.ChangeTeamMessage, values);
    }

    private void AnnouncePlayerTeamJoin(string playerName, int team)
    {
        if (string.IsNullOrEmpty(Config.JoinTeamMessage)) return;

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{PLAYERNAME}"] = playerName,
            ["{TEAM}"] = ColoredTeam(team)
        };

        Broadcast(Config.JoinTeamMessage, values);
    }

    /// Каждому игроку — на его языке: локализация идёт по SteamID получателя.
    private void Broadcast(string template, IReadOnlyDictionary<string, string> values)
    {
        foreach (var p in Utilities.GetPlayers().Where(u => u is { IsBot: false, IsValid: true }))
            _displayService.Print(MessageType.Chat, template, p, values);
    }

    private static HookResult EventPlayerTeamChangePre(EventPlayerTeam ev, GameEventInfo info)
    {
        info.DontBroadcast = true;
        return HookResult.Continue;
    }
}
