using System;
using Xunit;

namespace NotifyMessages.Tests;

/// Смена сторон приходит как player_team у всех игроков в одном кадре.
/// Переход одного игрока так не выглядит никогда.
public class TeamSwapTests
{
    [Fact]
    public void TwoOrMoreSideSwitchesInOneFrame_IsMassSwap()
    {
        Assert.True(TeamSwap.IsMassSwap(new[] { (2, 3), (3, 2), (2, 3), (3, 2) }));
        Assert.True(TeamSwap.IsMassSwap(new[] { (2, 3), (3, 2) }));
    }

    [Fact]
    public void SinglePlayer_IsNotMassSwap()
    {
        Assert.False(TeamSwap.IsMassSwap(new[] { (2, 3) }));
        Assert.False(TeamSwap.IsMassSwap(Array.Empty<(int, int)>()));
    }

    [Fact]
    public void BatchWithAJoinOrSpectatorMove_IsNotMassSwap()
    {
        // Кто-то вышел из спектаторов в тот же кадр — это не смена сторон
        Assert.False(TeamSwap.IsMassSwap(new[] { (2, 3), (1, 2) }));
        Assert.False(TeamSwap.IsMassSwap(new[] { (2, 3), (0, 3) }));
    }
}

/// Возврат после смены карты: второй player_connect_full у fully-connected игрока.
public class ReturningPlayerTests
{
    [Fact]
    public void FreshMark_IsTakenOnce()
    {
        var session = new SessionService();
        var now = DateTime.UtcNow;

        session.MarkReturning(1, now);

        Assert.True(session.TakeReturning(1, now.AddSeconds(5), TimeSpan.FromSeconds(90)));
        // Метка одноразовая: второй переход того же игрока — уже настоящий
        Assert.False(session.TakeReturning(1, now.AddSeconds(6), TimeSpan.FromSeconds(90)));
    }

    [Fact]
    public void ExpiredMark_IsDroppedAndNotHonoured()
    {
        // Игрок просидел в спектаторах дольше срока — его выход в команду настоящий
        var session = new SessionService();
        var now = DateTime.UtcNow;

        session.MarkReturning(1, now);

        Assert.False(session.TakeReturning(1, now.AddMinutes(5), TimeSpan.FromSeconds(90)));
    }

    [Fact]
    public void UnknownPlayer_IsNotReturning()
    {
        Assert.False(new SessionService().TakeReturning(42, DateTime.UtcNow, TimeSpan.FromSeconds(90)));
    }

    [Fact]
    public void Disconnect_ClearsTheMark()
    {
        var session = new SessionService();
        var now = DateTime.UtcNow;

        session.MarkReturning(1, now);
        session.RemoveFullyConnected(1);

        Assert.False(session.TakeReturning(1, now, TimeSpan.FromSeconds(90)));
    }
}
