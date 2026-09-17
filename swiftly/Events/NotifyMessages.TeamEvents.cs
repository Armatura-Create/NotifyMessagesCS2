using System;
using System.Collections.Generic;
using System.Linq;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;

namespace NotifyMessages;

/// Игровые события, связанные со сменой команд
public sealed partial class NotifyMessages
{
    /// Сколько после возврата на новую карту первое попадание в команду считается
    /// не событием. Дольше — игрок сидел в спектаторах и его выход в команду настоящий.
    private static readonly TimeSpan ReturningGrace = TimeSpan.FromSeconds(90);

    // Переходы T<->CT, накопленные за кадр: анонс откладывается на один кадр, чтобы
    // отличить смену сторон (все сразу) от перехода одного игрока. Через границу кадра
    // уходят только строки и числа — IPlayer не переживает кадр.
    private readonly List<(string Name, int OldTeam, int NewTeam)> _pendingTeamChanges = new();
    private bool _teamFlushScheduled;

    private HookResult OnPlayerTeam(EventPlayerTeam ev)
    {
        if (ev.IsBot) return HookResult.Continue;

        var player = ev.UserIdPlayer;
        if (player is null || player.IsFakeClient || !player.IsValid) return HookResult.Continue;

        var newTeam = ev.Team;
        var oldTeam = ev.OldTeam;
        var playerName = player.Name;
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
            Core.Scheduler.NextTick(FlushTeamChanges);
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

        // Значения уходят в ProcessMessage и подставляются ДО рендера — иначе игрок
        // увидел бы в чате литеральное «{RED}Terrorists{DEFAULT}».
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
        foreach (var p in Core.PlayerManager.GetAllPlayers())
        {
            if (p.IsFakeClient || !p.IsValid) continue;
            _displayService.Print(MessageType.Chat, template, p, values);
        }
    }
}
