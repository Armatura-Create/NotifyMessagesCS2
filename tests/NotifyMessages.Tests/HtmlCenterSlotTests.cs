using Xunit;

namespace NotifyMessages.Tests;

/// Слоты в CS2 переиспользуются: индекс ушедшего игрока движок отдаёт следующему.
/// Без сверки владельца новый игрок увидел бы чужое HTML-сообщение, а обход слотов
/// напрямую (GetPlayerFromSlot) вообще ронял сервер на чужой сущности.
public class HtmlCenterSlotTests
{
    private static User Owned(ulong steamId, int printTime = 0) =>
        new() { HtmlPrint = true, SteamId = steamId, PrintTime = printTime, Message = "msg" };

    [Fact]
    public void KeepsShowing_WhileOwnerIsPresentAndTimeIsLeft()
    {
        Assert.False(DisplayService.ShouldStopShowing(Owned(76561198000000001), 76561198000000001,
            elapsedSeconds: 1.0f, durationSeconds: 5f));
    }

    [Fact]
    public void StopsShowing_WhenDurationElapsed()
    {
        Assert.True(DisplayService.ShouldStopShowing(Owned(76561198000000001), 76561198000000001,
            elapsedSeconds: 5.0f, durationSeconds: 5f));
    }

    [Fact]
    public void StopsShowing_WhenSlotWasTakenByAnotherPlayer()
    {
        // Тот же слот, другой SteamID — сообщение предыдущего владельца показывать нельзя
        Assert.True(DisplayService.ShouldStopShowing(Owned(76561198000000001), 76561198000000002,
            elapsedSeconds: 0f, durationSeconds: 5f));
    }

    [Fact]
    public void StopsShowing_ForFreshSlotStateWithNoOwner()
    {
        // Слот, в который ещё никто не писал (SteamId == 0), не должен ничего рисовать
        Assert.True(DisplayService.ShouldStopShowing(new User { HtmlPrint = true }, 76561198000000001,
            elapsedSeconds: 0f, durationSeconds: 5f));
    }

    [Fact]
    public void MessageType_MapsToOutputChannel()
    {
        Assert.Equal(CounterStrikeSharp.API.Modules.Utils.HudDestination.Chat,
            DisplayService.ToHudDestination(MessageType.Chat));
        Assert.Equal(CounterStrikeSharp.API.Modules.Utils.HudDestination.Console,
            DisplayService.ToHudDestination(MessageType.Console));
        Assert.Equal(CounterStrikeSharp.API.Modules.Utils.HudDestination.Alert,
            DisplayService.ToHudDestination(MessageType.Alert));
        Assert.Equal(CounterStrikeSharp.API.Modules.Utils.HudDestination.Center,
            DisplayService.ToHudDestination(MessageType.Center));
        Assert.Equal(CounterStrikeSharp.API.Modules.Utils.HudDestination.Center,
            DisplayService.ToHudDestination(MessageType.CenterHtml));
    }
}
