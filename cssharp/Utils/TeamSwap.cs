using System.Collections.Generic;

namespace NotifyMessages;

/// Отличает смену сторон (halftime, mp_swapteams) от перехода одного игрока.
///
/// Движок меняет стороны всем игрокам синхронно, в одном кадре, и каждому шлёт
/// player_team с T<->CT. Один игрок так не ходит: он переходит один. Поэтому пачка
/// из двух и более T<->CT за один кадр — это смена сторон, и анонсировать её нечего.
///
/// Это вторая линия обороны рядом с полем player_team.silent: то поле движок выставляет
/// при своих «тихих» сменах команды (штатного сообщения в чате при этом нет), но оно
/// нигде не документировано, и класть весь фильтр на него одно нельзя.
public static class TeamSwap
{
    private const int Terrorists = 2;
    private const int CounterTerrorists = 3;

    public static bool IsSideSwitch(int oldTeam, int newTeam)
        => (oldTeam == Terrorists && newTeam == CounterTerrorists) ||
           (oldTeam == CounterTerrorists && newTeam == Terrorists);

    /// Пачка переходов, пришедших за один кадр, — смена сторон?
    public static bool IsMassSwap(IReadOnlyList<(int OldTeam, int NewTeam)> changes)
    {
        if (changes == null || changes.Count < 2) return false;

        foreach (var (oldTeam, newTeam) in changes)
        {
            if (!IsSideSwitch(oldTeam, newTeam)) return false;
        }

        return true;
    }
}
